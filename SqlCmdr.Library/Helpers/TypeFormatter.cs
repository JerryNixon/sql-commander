namespace SqlCmdr.Helpers;

internal static class TypeFormatter
{
    public static string FormatDataType(string dataType, int? maxLength, int? precision, int? scale)
    {
        var baseType = dataType.ToLower();
        return baseType switch
        {
            "varchar" or "char" or "nvarchar" or "nchar" when maxLength > 0 => 
                $"{dataType}({(maxLength == -1 ? "max" : maxLength.ToString())})",
            "decimal" or "numeric" when precision.HasValue => 
                $"{dataType}({precision},{scale ?? 0})",
            _ => dataType
        };
    }
}
