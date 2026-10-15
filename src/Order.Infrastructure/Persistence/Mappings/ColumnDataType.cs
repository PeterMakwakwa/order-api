namespace Order.Infrastructure.Persistence.Mappings
{
    public static class ColumnDataType
    {
        public const string Int = "int";
        public const string BigInt = "bigint";

        public const string UniqueIdentifier = "uniqueidentifier";

        public const string Bit = "bit";
        public const string Char64 = "char(64)";
        public const string Varchar32 = "varchar(32)";
        public const string Varchar50 = "varchar(50)";
        public const string Varchar100 = "varchar(100)";

        public const string Nvarchar200 = "nvarchar(200)";
        public const string Nvarchar320 = "nvarchar(320)";
        public const string Nvarchar500 = "nvarchar(500)";
        public const string Nvarchar2000 = "nvarchar(2000)";
        public const string NvarcharMax = "nvarchar(max)";

        public const string DateTimeOffset = "datetimeoffset";

        public const string Money = "decimal(18, 2)";
        public const string Rate = "decimal(5, 4)";
    }
}