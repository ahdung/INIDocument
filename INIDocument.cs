using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace AhDung
{
    /// <summary>
    /// INI�ĵ�
    /// </summary>
    [Serializable]
    public class INIDocument : Dictionary<string, INISection>, IEnumerable<INISection>, ISerializable
    {
        private StringCollection _comments;

        /// <summary>
        /// �ĵ�ע������
        /// </summary>
        private StringCollection Comments
        {
            get { return _comments ?? (_comments = new StringCollection()); }
        }

        /// <summary>
        /// ��ȡ�ڼ���
        /// </summary>
        public ICollection<INISection> Sections
        {
            get { return this.Values; }
        }

        /// <summary>
        /// ��ȡ�ڡ��������ڷ���null
        /// </summary>
        public new INISection this[string sectionName]
        {
            get
            {
                INISection r;
                TryGetValue(sectionName, out r);
                return r;
            }
        }

        /// <summary>
        /// ��ȡ������ֵ�����ڻ�������ڣ�����null����ֵ���Զ����
        /// </summary>
        /// <param name="section">�ڵ�</param>
        /// <param name="key">��</param>
        public string this[string section, string key]
        {
            get
            {
                INISection s;
                return this.TryGetValue(section, out s) ? s[key] : null;
            }
            set
            {
                INISection s;
                if (!TryGetValue(section, out s))
                {
                    s = this.Add(section);
                }

                s[key] = value;
            }
        }

        /// <summary>
        /// �����л����캯��
        /// </summary>
        protected INIDocument(SerializationInfo info, StreamingContext context)
            : this()
        {
            this.LoadText(info.GetString("body"));
        }

        /// <summary>
        /// ��ʼ��INI�ĵ���
        /// </summary>
        public INIDocument()
            : base(StringComparer.OrdinalIgnoreCase)
        { }

        /// <summary>
        /// ��ָ���ļ���ʼ��INI�ĵ���
        /// </summary>
        public INIDocument(string iniFile)
            : this()
        {
            this.Load(iniFile);
        }

        /// <summary>
        /// ���ļ�����INI���ݡ��������
        /// </summary>
        public void Load(string iniFile)
        {
            this.Load(iniFile, Encoding.Default);
        }

        /// <summary>
        /// ���ļ�����INI���ݡ��������
        /// </summary>
        /// <param name="iniFile">�ļ�·��</param>
        /// <param name="encoding">����</param>
        public void Load(string iniFile, Encoding encoding)
        {
            if (iniFile == null || iniFile.Trim().Length == 0) { throw new ArgumentNullException(); }
            if (!File.Exists(iniFile)) { throw new FileNotFoundException(); }

            this.LoadCore(File.OpenRead(iniFile), encoding);
        }

        /// <summary>
        /// ��������INI���ݡ��������
        /// </summary>
        public void Load(Stream stream)
        {
            this.Load(stream, Encoding.Default);
        }

        /// <summary>
        /// ��������INI���ݡ��������
        /// </summary>
        /// <param name="stream">������</param>
        /// <param name="encoding">����</param>
        public void Load(Stream stream, Encoding encoding)
        {
            if (stream == null || !stream.CanRead) { throw new ArgumentException("stream"); }

            this.LoadCore(stream, encoding);
        }

        /// <summary>
        /// ֱ������ini�ı�
        /// </summary>
        /// <param name="text"></param>
        public void LoadText(string text)
        {
            this.LoadCore(text, null);
        }

        /// <summary>
        /// �������뷽��
        /// </summary>
        private void LoadCore(object streamOrText, Encoding encoding)
        {
            if (_comments != null) { _comments.Clear(); }
            this.Clear();//�����

            INISection currSection = null;

            using (TextReader reader = streamOrText is Stream
                                       ? (TextReader)new StreamReader((Stream)streamOrText, encoding)
                                       : new StringReader((string)streamOrText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    //��������
                    if (line.Length == 0) { continue; }

                    //��¼ע���У�;#��ͷ��
                    if (line.StartsWith(";") || line.StartsWith("#"))
                    {
                        if (currSection == null) { Comments.Add(line); }
                        else { currSection.Comments.Add(line); }
                        continue;
                    }

                    //û�нڵļ�ֵ��
                    if (currSection == null && !line.StartsWith("[")) { throw new INIFormatException("��ֵ�������κνڣ�"); }

                    if (line.StartsWith("=")) { throw new INIFormatException("��Ϊ�գ�"); }

                    if (line.StartsWith("["))//�ڴ���
                    {
                        string section = GetInnerString(line, '[', ']');
                        if (section.Trim().Length == 0) { throw new INIFormatException("����Ϊ�գ�"); }

                        currSection = new INISection(section);//���Ϸ������쳣
                        try
                        { this.Add(section, currSection); }
                        catch (ArgumentException ex)
                        { throw new INIFormatException(string.Format("�� {0} �ظ���", section), ex); }
                    }
                    else//��ֵ����
                    {
                        string[] kv = line.Split(new[] { '=' }, 2);//�Ե�һ��=�ŷָ���Ӧ��key=adKH==�����
                        if (kv.Length != 2) { throw new INIFormatException(string.Format("�С�{0}����ʽ��Ч��", line)); }

                        try
                        { currSection.Add(kv[0].Trim(), kv[1].Trim()); }
                        catch (ArgumentException ex)
                        { throw new INIFormatException(string.Format("�� {0} �еļ� {1} �ظ���", currSection.Name, kv[0].Trim()), ex); }
                    }
                }
            }
        }

        /// <summary>
        /// ���浽�ļ���Ĭ�ϱ���
        /// </summary>
        /// <param name="iniFile">�ļ�·��</param>
        /// <param name="insertLineBetweenSections">�Ƿ��ڽ�֮����һ������</param>
        /// <param name="withComments">�Ƿ������ע</param>
        public void Save(string iniFile, bool insertLineBetweenSections = true, bool withComments = true)
        {
            this.Save(iniFile, Encoding.Default, insertLineBetweenSections, withComments);
        }

        /// <summary>
        /// ���浽�ļ���ָ������
        /// </summary>
        /// <param name="iniFile">�ļ�·��</param>
        /// <param name="encoding">����</param>
        /// <param name="insertLineBetweenSections">�Ƿ��ڽ�֮����һ������</param>
        /// <param name="withComments">�Ƿ������ע</param>
        public void Save(string iniFile, Encoding encoding, bool insertLineBetweenSections = true, bool withComments = true)
        {
            if (iniFile == null || iniFile.Trim().Length == 0) { throw new ArgumentNullException("iniFile"); }

            //���Բ�����File.OpenWrite��������������ļ�
            using (Stream stream = File.Open(iniFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                this.SaveCore(stream, encoding, insertLineBetweenSections, withComments);
            }
        }

        /// <summary>
        /// ���浽����Ĭ�ϱ���
        /// </summary>
        /// <param name="stream">Ҫ�������</param>
        /// <param name="insertLineBetweenSections">�Ƿ��ڽ�֮����һ������</param>
        /// <param name="withComments">�Ƿ������ע</param>
        public void Save(Stream stream, bool insertLineBetweenSections = true, bool withComments = true)
        {
            this.Save(stream, Encoding.Default, insertLineBetweenSections, withComments);
        }

        /// <summary>
        /// ���浽����ָ������
        /// </summary>
        /// <param name="stream">Ҫ�������</param>
        /// <param name="encoding">����</param>
        /// <param name="insertLineBetweenSections">�Ƿ��ڽ�֮����һ������</param>
        /// <param name="withComments">�Ƿ������ע</param>
        public void Save(Stream stream, Encoding encoding, bool insertLineBetweenSections = true, bool withComments = true)
        {
            this.SaveCore(stream, encoding, insertLineBetweenSections, withComments);
        }

        /// <summary>
        /// ���ı��淽��
        /// </summary>
        private void SaveCore(Stream stream, Encoding encoding, bool insertLineBetweenSections = true, bool withComments = true)
        {
            if (stream == null) { throw new ArgumentNullException("stream"); }

            using (StreamWriter writer = new StreamWriter(stream, encoding))
            {
                writer.Write(this.GetText(insertLineBetweenSections, withComments));
            }
        }

        /// <summary>
        /// ��ȡINI�ı�
        /// </summary>
        /// <param name="insertLineBetweenSections">�Ƿ��ڽ�֮����һ������</param>
        /// <param name="withComments">�Ƿ����ע��</param>
        public string GetText(bool insertLineBetweenSections = true, bool withComments = true)
        {
            if (Sections.Count == 0 && (_comments == null || _comments.Count == 0)) { return string.Empty; }

            //��ע
            StringBuilder sb = new StringBuilder();
            if (withComments && _comments != null && _comments.Count != 0)
            {
                foreach (string cmt in _comments)
                {
                    sb.AppendLine(cmt);
                }
            }

            //����
            foreach (INISection section in this)
            {
                if (insertLineBetweenSections) { sb.AppendLine(); }

                sb.Append(section.GetText(withComments));
            }

            return sb[0] == '\r'
                ? sb.ToString(Environment.NewLine.Length, sb.Length - Environment.NewLine.Length) //ȥ�׸�����
                : sb.ToString();
        }

        /// <summary>
        /// �޸Ľ�����ԭ�ڲ�������ʲôҲ����
        /// </summary>
        public void ChangeSectionName(string currName, string newName)
        {
            if (!INISection.IsWellSectionOrKeyName(newName)) { throw new ArgumentException("�½�����Ч��"); }

            INISection val;
            if (!TryGetValue(currName, out val)) { return; }

            //�����Сд�޸�
            if (ContainsKey(newName) && !string.Equals(currName, newName, StringComparison.OrdinalIgnoreCase))
            { throw new ArgumentException("������ͬ������"); }

            Remove(currName);
            Add(newName, val);
            val.Name = newName;//����ҲҪ��
        }

        /// <summary>
        /// �ж��Ƿ����ָ����
        /// </summary>
        public bool Contains(string sectionName)
        {
            return ContainsKey(sectionName);
        }

        /// <summary>
        /// ����·������ֵ���ںͼ������ڻ��Զ�����
        /// </summary>
        public void Set(string path, string value)
        {
            string section, key;
            ParsePath(path, out section, out key);

            this[section, key] = value;
        }

        /// <summary>
        /// ����·������ֵ��T.ToString�����ںͼ������ڻ��Զ�����
        /// </summary>
        public void Set<T>(string path, T value)
        {
            Set(path, value == null ? null : value.ToString());
        }

        #region ��������

        /// <summary>
        /// ȡ������ĳ���ַ��е��ı�
        /// </summary>
        private static string GetInnerString(string s, char left, char right)
        {
            int idxL = s.IndexOf(left);
            int idxR = s.IndexOf(right, idxL + 1);
            if ((idxR - idxL) <= 1)
            {
                return string.Empty;
            }

            return s.Substring(idxL + 1, idxR - idxL - 1);
        }

        /// <summary>
        /// ����·�����ʽ
        /// </summary>
        private void ParsePath(string path, out string section, out string key)
        {
            if (path == null || path.Trim().Length == 0)
            {
                throw new ArgumentNullException();
            }

            string[] sectionAndKey = path.Split('/');
            if (sectionAndKey.Length != 2)
            {
                throw new FormatException("·�����ʽ��Ч��");
            }

            if (!INISection.IsWellSectionOrKeyName(sectionAndKey[0]) || !INISection.IsWellSectionOrKeyName(sectionAndKey[1]))
            {
                throw new ArgumentException(string.Format("�ڵ�����{0}���������{1}����Ч��", sectionAndKey[0], sectionAndKey[1]));
            }

            section = sectionAndKey[0];
            key = sectionAndKey[1];
        }

        #endregion

        #region ���ݱ��ʽֱ�ӻ�ȡֵ

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public string Get(string path, string defaultValue = "")
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.Get(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public T Get<T>(string path, T defaultValue = default(T))
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.Get(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public int GetInt32(string path, int defaultValue = 0)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetInt32(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public bool GetBoolean(string path, bool defaultValue = false)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetBoolean(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public DateTime? GetDateTime(string path, DateTime? defaultValue = null)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetDateTime(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public decimal GetDecimal(string path, decimal defaultValue = 0)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetDecimal(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// ����·�����ʽ��ȡֵ
        /// </summary>
        /// <param name="path">Section/Key����ʽ��ע������б��</param>
        /// <param name="defaultValue">�ڵ���������ʱ���ظ�ֵ</param>
        public Uri GetUri(string path, Uri defaultValue = null)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetUri(key, defaultValue) : defaultValue;
        }

        #endregion

        /// <summary>
        /// ��ӽڡ������Ѵ�����ʲôҲ����
        /// </summary>
        public INISection Add(string sectionName)
        {
            if (!INISection.IsWellSectionOrKeyName(sectionName))
            { throw new ArgumentException(); }

            INISection section;
            if (TryGetValue(sectionName, out section))
            {
                return section;
            }

            section = new INISection(sectionName);
            Add(section);
            return section;
        }

        /// <summary>
        /// ��ӽڡ������Ѵ�����ᱻ�滻
        /// </summary>
        public void Add(INISection section)
        {
            if (section == null) { throw new ArgumentNullException(); }
            base[section.Name] = section;
        }

        /// <summary>
        /// ���ƽڵ�ָ������
        /// </summary>
        public void CopyTo(INISection[] array, int arrayIndex)
        {
            this.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// �Ƴ�ָ����
        /// </summary>
        public new bool Remove(string sectionName)
        {
            return base.Remove(sectionName);
        }

        /// <summary>
        /// �Ƴ�ָ����
        /// </summary>
        public bool Remove(INISection section)
        {
            return section != null && base.Remove(section.Name);
        }

        //�滻���෵��KV������ֻ����V
        public new IEnumerator<INISection> GetEnumerator()
        {
            return this.Values.GetEnumerator();
        }

        /// <summary>
        /// ʵ�����л�
        /// </summary>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("body", this.GetText(false));
        }
    }

    /// <summary>
    /// INI��
    /// </summary>
    public class INISection : Dictionary<string, string>
    {
        StringCollection _comments;
        string _name;

        /// <summary>
        /// ��ע������
        /// </summary>
        internal StringCollection Comments
        {
            get { return _comments ?? (_comments = new StringCollection()); }
        }

        /// <summary>
        /// ��ȡ����
        /// </summary>
        public string Name
        {
            get { return _name; }

            //�����⿪��
            internal set
            {
                if (!IsWellSectionOrKeyName(value)) { throw new FormatException("������Ч��"); }

                if (value == _name) { return; }

                _name = value;
            }
        }

        /// <summary>
        /// ��ȡ������ָ������ֵ�������������򷵻�null����ֵ���Զ���Ӽ�
        /// </summary>
        public new string this[string key]
        {
            get
            {
                string r;
                TryGetValue(key, out r);
                return r;
            }
            set
            {
                if (!IsWellSectionOrKeyName(key)) { throw new ArgumentException("������Ч��"); }
                base[key] = value;
            }
        }

        /// <summary>
        /// ��ʼ����
        /// </summary>
        /// <param name="name">����</param>
        public INISection(string name)
            : base(StringComparer.OrdinalIgnoreCase)
        {
            Name = name;
        }

        /// <summary>
        /// ��ȡָ������ֵ�����������ڻ�ֵΪ��ʱ����defaultValue
        /// </summary>
        public T Get<T>(string key, T defaultValue = default(T))
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? (T)Convert.ChangeType(val, typeof(T))
                : defaultValue;
        }

        /// <summary>
        /// ��ȡָ������ֵ����ע�⡿�����������򷵻�defaultValue����ֵΪ���򷵻�string.Empty
        /// </summary>
        public string Get(string key, string defaultValue = "")
        {
            string val;
            return TryGetValue(key, out val)
                ? val
                : defaultValue;
        }

        /// <summary>
        /// ��ȡint�����������ڻ�ֵΪ��ʱ����defaultValue
        /// </summary>
        /// <param name="key">����</param>
        /// <param name="defaultValue">����������ʱ���ظ�ֵ</param>
        /// <returns>ֵΪ��ʱ�᳢��ת�������Կ��ܻ���ת���쳣</returns>
        public int GetInt32(string key, int defaultValue = 0)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? int.Parse(val)
                : defaultValue;
        }

        /// <summary>
        /// ��ȡbool���ɴ���1/0/true/false�����������ڻ�ֵΪ��ʱ����defaultValue
        /// </summary>
        public bool GetBoolean(string key, bool defaultValue = false)
        {
            string val; int valAsInt;
            return TryGetValue(key, out val) && val.Length != 0
                ? (int.TryParse(val, out valAsInt) ? Convert.ToBoolean(valAsInt) : Convert.ToBoolean(val))
                : defaultValue;
        }

        /// <summary>
        /// ��ȡdecimal�����������ڻ�ֵΪ��ʱ����defaultValue
        /// </summary>
        public decimal GetDecimal(string key, decimal defaultValue = 0)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? decimal.Parse(val)
                : defaultValue;
        }

        /// <summary>
        /// ��ȡDateTime?�����������ڻ�ֵΪ��ʱ����defaultValue
        /// </summary>
        public DateTime? GetDateTime(string key, DateTime? defaultValue = null)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? DateTime.Parse(val)
                : defaultValue;
        }

        /// <summary>
        /// ��ȡUri�����������ڻ�ֵΪ��ʱ����defaultValue
        /// </summary>
        public Uri GetUri(string key, Uri defaultValue = null)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? new Uri(val)
                : defaultValue;
        }

        /// <summary>
        /// ��Ӽ�ֵ�������Ѵ��������
        /// </summary>
        public new void Add(string key, string value)
        {
            this[key] = value;
        }

        /// <summary>
        /// ��ȡ���ı�
        /// </summary>
        public string GetText(bool withComments = true)
        {
            if (this.Count == 0 && (_comments == null || _comments.Count == 0)) { return string.Empty; }

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("[{0}]\r\n", Name);
            foreach (KeyValuePair<string, string> kv in this)
            {
                sb.AppendFormat("{0}={1}\r\n", kv.Key, kv.Value);
            }

            //д��ע
            if (withComments && _comments != null && _comments.Count != 0)
            {
                foreach (string cmt in _comments)
                {
                    sb.AppendLine(cmt);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// ȷ�������ַ����Ƿ��ǺϷ��Ľںͼ���
        /// </summary>
        public static bool IsWellSectionOrKeyName(string name)
        {
            return name != null && name.Trim().Length != 0
                && name.IndexOfAny(new[] { '[', ']', '/' }) == -1;
        }
    }
}