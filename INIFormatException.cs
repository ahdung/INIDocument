using System;

namespace AhDung
{
    /// <summary>
    /// INI��ʽ�쳣��
    /// </summary>
    public class INIFormatException : FormatException
    {
        public INIFormatException() : base("INI�ļ���ʽ��Ч��") { }

        public INIFormatException(string message) : base(message) { }

        public INIFormatException(string message, Exception innerException) : base(message, innerException) { }
    }
}