using System;

namespace AhDung
{
    /// <summary>
    /// INI格式异常类
    /// </summary>
    public class INIFormatException : FormatException
    {
        public INIFormatException() : base("INI文件格式无效！") { }

        public INIFormatException(string message) : base(message) { }

        public INIFormatException(string message, Exception innerException) : base(message, innerException) { }
    }
}