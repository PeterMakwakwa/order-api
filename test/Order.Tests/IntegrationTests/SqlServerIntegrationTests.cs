using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Order.Application.Constants;
using Order.Application.Handlers.Abstractions;
using Order.Application.Handlers.Exceptions;
using Order.Application.Handlers.Outbox;
using Order.Application.Handlers.Reconciliation;
using Order.Application.Models.Request.V1;
using Order.Application.Models.Response.V1;
using Order.Domain.Commands;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Interfaces.Email;
using Order.Domain.Interfaces.Email.Models;
using Order.Domain.Interfaces.Payments;
using Order.Domain.Interfaces.Payments.Models;
using Order.Domain.UnitOfWork;
using Order.Domain.UnitOfWork.Exceptions;
using Order.Infrastructure.Persistence.Context;
using Order.Infrastructure.Persistence.Extensions;
using Order.Tests.Builders;
using Order.Tests.Setup;
using Testcontainers.MsSql;
using Domain = Order.Domain.Entities;

namespace Order.Tests.IntegrationTests
{
    /// <summary>
    /// Runs the persistence layer against a real SQL Server whose schema is deployed from src/dbOrder by grate — the same
    /// image and arguments as docker-compose — so the SQL scripts and the EF Core mappings cannot drift apart unnoticed.
    /// The API connects as the least-privilege application login, exactly as in every environment.
    /// Ignored (not failed) when Docker is not available; CI runs it.
    /// </summary>
    [Category("Integration")]
    [NonParallelizable]
    public sealed class SqlServerIntegrationTests
    {
        private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
        private const string GrateImage = "erikbra/grate:2.1.6";
        private const string SqlNetworkAlias = "sql";
        private const string ApplicationLogin = "svc_order_api";

        // Generated per run (never committed); SQL Server password policy needs upper, lower, digit and symbol.
        private static readonly string SaPassword = $"Sa1!{Guid.NewGuid():N}";
        private static readonly string ApplicationPassword = $"App1!{Guid.NewGuid():N}";

        private INetwork? _network;
        private MsSqlContainer? _sqlContainer;
        private ServiceProvider? _serviceProvider;
        private IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
        private IEmailSender _emailSender = Substitute.For<IEmailSender>();

        [OneTimeSetUp]
        public async Task StartSqlServerAsync()
        {
            try
            {
                _network = new NetworkBuilder().Build();
            }
            catch (DockerUnavailableException ex)
            {
                Assert.Ignore($"Docker is not available, so the SQL Server integration tests were skipped: {ex.Message}");
            }

            await _network.CreateAsync();

            _sqlContainer = new MsSqlBuilder(SqlServerImage)
                .WithPassword(SaPassword)
                .WithNetwork(_network)
                .WithNetworkAliases(SqlNetworkAlias)
                .Build();
            await _sqlContainer.StartAsync();

            await DeployDatabaseAsync();

            _serviceProvider = UnitTestSetup.Setup(services =>
            {
                UseSqlServer(services);
                services.AddTransient(_ => PaymentGatewayResolver());
                services.AddTransient(_ => _emailSender);
            });
        }

        [OneTimeTearDown]
        public async Task StopSqlServerAsync()
        {
            _serviceProvider?.Dispose();

            if (_sqlContainer != null)
            {
                await _sqlContainer.DisposeAsync();
            }

            if (_network != null)
            {
                await _network.DisposeAsync();
            }
        }

        [SetUp]
        public void ResetSubstitutes()
        {
            _paymentGateway.ClearReceivedCalls();
            _paymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Succeeded());
            _emailSender.ClearReceivedCalls();
        }

        [Test]
        public async Task PlaceOrder_ThenRead_RoundTripsThroughSqlServer()
        {
            // Assemble: products and WELCOME10 come from the *.ENV.LOCAL.sql seed scripts.
            int customerId = NewCustomerId();

            // Act
            PlaceOrderResponse placed = await PlaceOrderAsync(customerId, Guid.NewGuid().ToString());
            GetOrderResponse order = await Handler<GetOrderRequest, GetOrderResponse>().HandleAsync(new GetOrderRequest { OrderId = placed.OrderId });
            GetCustomerOrderHistoryResponse history = await Handler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse>()
                .HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = customerId });

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(placed.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(placed.Total, Is.EqualTo(PlaceOrderRequestBuilder.DefaultTotal));
                Assert.That(order.CustomerId, Is.EqualTo(customerId));
                Assert.That(order.PromotionCode, Is.EqualTo(DefaultPromotions.Welcome10.Code));
                Assert.That(order.DiscountRate, Is.EqualTo(0.1m));
                Assert.That(order.Items, Has.Count.EqualTo(2));
                Assert.That(history.Orders.Single().Id, Is.EqualTo(placed.OrderId));
            }
        }

        [Test]
        public async Task PlaceOrder_SameIdempotencyKey_ReplaysFromSqlServer()
        {
            // Assemble
            int customerId = NewCustomerId();
            string idempotencyKey = Guid.NewGuid().ToString();
            PlaceOrderResponse first = await PlaceOrderAsync(customerId, idempotencyKey);

            // Act
            PlaceOrderResponse replay = await PlaceOrderAsync(customerId, idempotencyKey);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(replay.IsReplay, Is.True);
                Assert.That(replay.OrderId, Is.EqualTo(first.OrderId));
            }

            await _paymentGateway.ReceivedWithAnyArgs(1).ChargeAsync(default!, default);
        }

        [Test]
        public async Task SaveOrder_DuplicateIdempotencyKey_ThrowsUniqueConstraintViolation()
        {
            // Assemble: two concurrent requests that both passed the "already placed?" check.
            CreateOrderCommandBuilder builder = new CreateOrderCommandBuilder().WithCustomerId(NewCustomerId()).WithIdempotencyKey(Guid.NewGuid().ToString());
            await SaveOrderAsync(new Domain::Order(builder.Build()));

            // Act & Assert
            Assert.ThrowsAsync<UniqueConstraintViolationException>(() => SaveOrderAsync(new Domain::Order(builder.Build())));
        }

        [Test]
        public async Task Outbox_ClaimRace_OnlyOneProcessorWins_AndMessageIsDelivered()
        {
            // Assemble
            PlaceOrderResponse placed = await PlaceOrderAsync(NewCustomerId(), Guid.NewGuid().ToString());
            long messageId = await GetOutboxMessageIdAsync(placed.OrderId);
            IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();

            using (IOrderUnitOfWork first = unitOfWorkFactory.Create())
            using (IOrderUnitOfWork second = unitOfWorkFactory.Create())
            {
                OrderPlacedOutboxMessage firstCopy = (await first.OrderPlacedOutboxRepository.GetByIdAsync(messageId))!;
                OrderPlacedOutboxMessage secondCopy = (await second.OrderPlacedOutboxRepository.GetByIdAsync(messageId))!;

                // Act
                firstCopy.Claim(DateTimeOffset.UtcNow.AddSeconds(-1));
                await first.SaveChangesAsync();
                secondCopy.Claim(DateTimeOffset.UtcNow.AddMinutes(5));

                // Assert
                Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
            }

            // The first claim's lease has expired, so the processor takes the message over and delivers it.
            OrderPlacedOutboxMessageHandler outboxHandler = ActivatorUtilities.CreateInstance<OrderPlacedOutboxMessageHandler>(_serviceProvider!);
            await outboxHandler.ProcessPendingMessagesAsync(CancellationToken.None);

            await _emailSender.Received().SendAsync(Arg.Is<EmailMessage>(email => email.Body.Contains(placed.OrderId.ToString())), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task OrderHistory_KeysetPaging_WalksEveryOrderOnceOnSqlServer()
        {
            // Assemble
            int customerId = NewCustomerId();
            List<Guid> placed = [];
            for (int i = 0; i < 5; i++)
            {
                placed.Add((await PlaceOrderAsync(customerId, Guid.NewGuid().ToString())).OrderId);
            }

            // Act
            List<Guid> seen = [];
            string? cursor = null;
            int pages = 0;
            do
            {
                GetCustomerOrderHistoryResponse page = await Handler<GetCustomerOrderHistoryRequest, GetCustomerOrderHistoryResponse>()
                    .HandleAsync(new GetCustomerOrderHistoryRequest { CustomerId = customerId, PageSize = 2, Cursor = cursor });
                seen.AddRange(page.Orders.Select(order => order.Id));
                cursor = page.NextCursor;
                pages++;
            }
            while (cursor != null);

            // Assert
            using (Assert.EnterMultipleScope())
            {
                Assert.That(seen, Is.EqualTo(Enumerable.Reverse(placed)));
                Assert.That(pages, Is.EqualTo(3));
            }
        }

        [Test]
        public async Task RetryPayment_TwoRetriesAtOnce_OnlyOneSavesOnSqlServer()
        {
            // Assemble: a provider error, then two retries that both loaded the order.
            int customerId = NewCustomerId();
            string idempotencyKey = Guid.NewGuid().ToString();
            _paymentGateway.ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>()).Returns(PaymentResult.Failed(PaymentOutcome.ProviderError, "Provider down"));
            Assert.ThrowsAsync<HandlerException>(() => PlaceOrderAsync(customerId, idempotencyKey));
            IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();

            using (IOrderUnitOfWork first = unitOfWorkFactory.Create())
            using (IOrderUnitOfWork second = unitOfWorkFactory.Create())
            {
                Domain::Order firstCopy = (await first.OrderRepository.GetByIdempotencyKeyAsync(customerId, idempotencyKey))!;
                Domain::Order secondCopy = (await second.OrderRepository.GetByIdempotencyKeyAsync(customerId, idempotencyKey))!;
                firstCopy.RetryPayment(DateTimeOffset.UtcNow);
                secondCopy.RetryPayment(DateTimeOffset.UtcNow);

                // Act
                await first.SaveChangesAsync();

                // Assert
                Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
            }
        }

        [Test]
        public async Task Reconciliation_StalledOrder_IsChargedAndSettledOnSqlServer()
        {
            // Assemble: an order its request abandoned between charging and recording the outcome.
            Domain::Order stalled = new(new CreateOrderCommandBuilder().WithCustomerId(NewCustomerId()).WithCreatedDate(DateTimeOffset.UtcNow.AddHours(-1)).Build());
            await SaveOrderAsync(stalled);

            // Act
            await ReconciliationHandler().ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert
            GetOrderResponse order = await Handler<GetOrderRequest, GetOrderResponse>().HandleAsync(new GetOrderRequest { OrderId = stalled.UniqueId });
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
                Assert.That(await CountOutboxMessagesAsync(stalled.UniqueId), Is.EqualTo(1));
            }
        }

        [Test]
        public async Task Reconciliation_OrderSettledElsewhereWhileCharging_RollsBackTheOutboxMessageOnSqlServer()
        {
            // Assemble: while the reconciliation charges, another instance settles the order first.
            Domain::Order stalled = new(new CreateOrderCommandBuilder().WithCustomerId(NewCustomerId()).WithCreatedDate(DateTimeOffset.UtcNow.AddHours(-1)).Build());
            await SaveOrderAsync(stalled);
            IUnitOfWorkFactory<IOrderUnitOfWork> unitOfWorkFactory = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>();
            _paymentGateway
                .ChargeAsync(Arg.Any<PaymentRequest>(), Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    using (IOrderUnitOfWork other = unitOfWorkFactory.Create())
                    {
                        Domain::Order otherCopy = (await other.OrderRepository.GetByUniqueIdAsync(stalled.UniqueId))!;
                        otherCopy.Process(new RecordPaymentOutcome { Outcome = PaymentOutcome.Rejected, FailureReason = "Settled elsewhere" });
                        await other.SaveChangesAsync();
                    }

                    return PaymentResult.Succeeded();
                });

            // Act
            await ReconciliationHandler().ReconcileStalledPaymentsAsync(CancellationToken.None);

            // Assert: the order keeps the other outcome, and the outbox row saved with the losing outcome was rolled back.
            GetOrderResponse order = await Handler<GetOrderRequest, GetOrderResponse>().HandleAsync(new GetOrderRequest { OrderId = stalled.UniqueId });
            using (Assert.EnterMultipleScope())
            {
                Assert.That(order.Status, Is.EqualTo(OrderStatus.PaymentFailed));
                Assert.That(await CountOutboxMessagesAsync(stalled.UniqueId), Is.Zero);
            }
        }

        private async Task DeployDatabaseAsync()
        {
            IContainer grate = new ContainerBuilder(GrateImage)
                .WithNetwork(_network!)
                .WithResourceMapping(new DirectoryInfo(Path.Combine(FindRepositoryRoot(), "src", "dbOrder")), "/db/")
                .WithEntrypoint("./grate")
                .WithCommand(
                    // Pooling=false: grate connects to dbOrder before creating it, and the connection pool would keep returning
                    // that failed login ("Cannot open database") for a few seconds, failing every first deployment.
                    $"--connstring=Server={SqlNetworkAlias};Database=dbOrder;User Id=sa;Password={SaPassword};TrustServerCertificate=True;Pooling=false",
                    "--sqlfilesdirectory=/db",
                    "--version=1.0.0",
                    "--databasetype=sqlserver",
                    "--silent",
                    "--outputPath=/tmp/grate",
                    "--createdatabase=true",
                    "--environment=LOCAL",
                    "--transaction=false",
                    $"--ut=AppLogin={ApplicationLogin}",
                    $"--ut=AppLoginPassword={ApplicationPassword}")
                .Build();

            await using (grate)
            {
                await grate.StartAsync();
                long exitCode = await grate.GetExitCodeAsync();
                if (exitCode != 0)
                {
                    (string stdout, string stderr) = await grate.GetLogsAsync();
                    Assert.Fail($"grate failed with exit code {exitCode}:{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
                }
            }
        }

        /// <summary>
        /// Replaces test mode's in-memory database with the container, connecting as the application login.
        /// </summary>
        private void UseSqlServer(IServiceCollection services)
        {
            services.RemoveAll<IDbContextFactory<OrderDatabaseContext>>();
            services.RemoveAll<DbContextOptions<OrderDatabaseContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OrderDatabaseContext>>();

            string connectionString = $"Server={_sqlContainer!.Hostname},{_sqlContainer.GetMappedPublicPort(MsSqlBuilder.MsSqlPort)};Database=dbOrder;"
                + $"User Id={ApplicationLogin};Password={ApplicationPassword};TrustServerCertificate=True";
            services.AddDatabase<OrderDatabaseContext>(connectionString);
        }

        private IPaymentGatewayResolver PaymentGatewayResolver()
        {
            _paymentGateway.Name.Returns(PaymentGatewayNames.Card);
            IPaymentGatewayResolver resolver = Substitute.For<IPaymentGatewayResolver>();
            resolver.Resolve(Arg.Any<PaymentMethods>()).Returns(_paymentGateway);

            return resolver;
        }

        private Task<PlaceOrderResponse> PlaceOrderAsync(int customerId, string idempotencyKey)
        {
            return Handler<PlaceOrderRequest, PlaceOrderResponse>()
                .HandleAsync(new PlaceOrderRequestBuilder().WithCustomerId(customerId).Build(), TestHeaders.Create(idempotencyKey: idempotencyKey));
        }

        private async Task SaveOrderAsync(Domain::Order order)
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                await unitOfWork.OrderRepository.AddAsync(order);
                await unitOfWork.SaveChangesAsync();
            }
        }

        private async Task<long> GetOutboxMessageIdAsync(Guid orderId)
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                return (await unitOfWork.OrderPlacedOutboxRepository.FindAsync(message => message.Message.Contains(orderId.ToString()))).Single().Id;
            }
        }

        private async Task<int> CountOutboxMessagesAsync(Guid orderId)
        {
            using (IOrderUnitOfWork unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWorkFactory<IOrderUnitOfWork>>().Create())
            {
                return (await unitOfWork.OrderPlacedOutboxRepository.FindAsync(message => message.Message.Contains(orderId.ToString()))).Count();
            }
        }

        private StalledPaymentReconciliationHandler ReconciliationHandler()
        {
            return ActivatorUtilities.CreateInstance<StalledPaymentReconciliationHandler>(_serviceProvider!);
        }

        private IRequestHandler<TRequest, TResponse> Handler<TRequest, TResponse>()
            where TRequest : class
            where TResponse : class
        {
            return _serviceProvider!.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        }

        private static int NewCustomerId()
        {
            return Random.Shared.Next(1_000, int.MaxValue);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Order.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root (Order.slnx).");
        }
    }
}