using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace AhDung
{
    /// <summary>
    /// INI文档
    /// </summary>
    [Serializable]
    public class INIDocument : Dictionary<string, INISection>, IEnumerable<INISection>, ISerializable
    {
        private StringCollection _comments;

        /// <summary>
        /// 文档注释容器
        /// </summary>
        private StringCollection Comments
        {
            get { return _comments ?? (_comments = new StringCollection()); }
        }

        /// <summary>
        /// 获取节集合
        /// </summary>
        public ICollection<INISection> Sections
        {
            get { return this.Values; }
        }

        /// <summary>
        /// 获取节。若不存在返回null
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
        /// 获取或设置值。若节或键不存在，返回null；赋值会自动添加
        /// </summary>
        /// <param name="section">节点</param>
        /// <param name="key">键</param>
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
        /// 反序列化构造函数
        /// </summary>
        protected INIDocument(SerializationInfo info, StreamingContext context)
            : this()
        {
            this.LoadText(info.GetString("body"));
        }

        /// <summary>
        /// 初始化INI文档类
        /// </summary>
        public INIDocument()
            : base(StringComparer.OrdinalIgnoreCase)
        { }

        /// <summary>
        /// 从指定文件初始化INI文档类
        /// </summary>
        public INIDocument(string iniFile)
            : this()
        {
            this.Load(iniFile);
        }

        /// <summary>
        /// 从文件载入INI内容。会先清空
        /// </summary>
        public void Load(string iniFile)
        {
            this.Load(iniFile, Encoding.Default);
        }

        /// <summary>
        /// 从文件载入INI内容。会先清空
        /// </summary>
        /// <param name="iniFile">文件路径</param>
        /// <param name="encoding">编码</param>
        public void Load(string iniFile, Encoding encoding)
        {
            if (iniFile == null || iniFile.Trim().Length == 0) { throw new ArgumentNullException(); }
            if (!File.Exists(iniFile)) { throw new FileNotFoundException(); }

            this.LoadCore(File.OpenRead(iniFile), encoding);
        }

        /// <summary>
        /// 从流载入INI内容。会先清空
        /// </summary>
        public void Load(Stream stream)
        {
            this.Load(stream, Encoding.Default);
        }

        /// <summary>
        /// 从流载入INI内容。会先清空
        /// </summary>
        /// <param name="stream">基础流</param>
        /// <param name="encoding">编码</param>
        public void Load(Stream stream, Encoding encoding)
        {
            if (stream == null || !stream.CanRead) { throw new ArgumentException("stream"); }

            this.LoadCore(stream, encoding);
        }

        /// <summary>
        /// 直接载入ini文本
        /// </summary>
        /// <param name="text"></param>
        public void LoadText(string text)
        {
            this.LoadCore(text, null);
        }

        /// <summary>
        /// 核心载入方法
        /// </summary>
        private void LoadCore(object streamOrText, Encoding encoding)
        {
            if (_comments != null) { _comments.Clear(); }
            this.Clear();//先清空

            INISection currSection = null;

            using (TextReader reader = streamOrText is Stream
                                       ? (TextReader)new StreamReader((Stream)streamOrText, encoding)
                                       : new StringReader((string)streamOrText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    //跳过空行
                    if (line.Length == 0) { continue; }

                    //记录注释行（;#开头）
                    if (line.StartsWith(";") || line.StartsWith("#"))
                    {
                        if (currSection == null) { Comments.Add(line); }
                        else { currSection.Comments.Add(line); }
                        continue;
                    }

                    //没有节的键值对
                    if (currSection == null && !line.StartsWith("[")) { throw new INIFormatException("键值不属于任何节！"); }

                    if (line.StartsWith("=")) { throw new INIFormatException("键为空！"); }

                    if (line.StartsWith("["))//节处理
                    {
                        string section = GetInnerString(line, '[', ']');
                        if (section.Trim().Length == 0) { throw new INIFormatException("节名为空！"); }

                        currSection = new INISection(section);//不合法会抛异常
                        try
                        { this.Add(section, currSection); }
                        catch (ArgumentException ex)
                        { throw new INIFormatException(string.Format("节 {0} 重复！", section), ex); }
                    }
                    else//键值处理
                    {
                        string[] kv = line.Split(new[] { '=' }, 2);//以第一个=号分隔，应对key=adKH==的情况
                        if (kv.Length != 2) { throw new INIFormatException(string.Format("行【{0}】格式无效！", line)); }

                        try
                        { currSection.Add(kv[0].Trim(), kv[1].Trim()); }
                        catch (ArgumentException ex)
                        { throw new INIFormatException(string.Format("节 {0} 中的键 {1} 重复！", currSection.Name, kv[0].Trim()), ex); }
                    }
                }
            }
        }

        /// <summary>
        /// 保存到文件。默认编码
        /// </summary>
        /// <param name="iniFile">文件路径</param>
        /// <param name="insertLineBetweenSections">是否在节之间留一个空行</param>
        /// <param name="withComments">是否包含备注</param>
        public void Save(string iniFile, bool insertLineBetweenSections = true, bool withComments = true)
        {
            this.Save(iniFile, Encoding.Default, insertLineBetweenSections, withComments);
        }

        /// <summary>
        /// 保存到文件。指定编码
        /// </summary>
        /// <param name="iniFile">文件路径</param>
        /// <param name="encoding">编码</param>
        /// <param name="insertLineBetweenSections">是否在节之间留一个空行</param>
        /// <param name="withComments">是否包含备注</param>
        public void Save(string iniFile, Encoding encoding, bool insertLineBetweenSections = true, bool withComments = true)
        {
            if (iniFile == null || iniFile.Trim().Length == 0) { throw new ArgumentNullException("iniFile"); }

            //绝对不能用File.OpenWrite，那样不会清空文件
            using (Stream stream = File.Open(iniFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                this.SaveCore(stream, encoding, insertLineBetweenSections, withComments);
            }
        }

        /// <summary>
        /// 保存到流。默认编码
        /// </summary>
        /// <param name="stream">要保存的流</param>
        /// <param name="insertLineBetweenSections">是否在节之间留一个空行</param>
        /// <param name="withComments">是否包含备注</param>
        public void Save(Stream stream, bool insertLineBetweenSections = true, bool withComments = true)
        {
            this.Save(stream, Encoding.Default, insertLineBetweenSections, withComments);
        }

        /// <summary>
        /// 保存到流。指定编码
        /// </summary>
        /// <param name="stream">要保存的流</param>
        /// <param name="encoding">编码</param>
        /// <param name="insertLineBetweenSections">是否在节之间留一个空行</param>
        /// <param name="withComments">是否包含备注</param>
        public void Save(Stream stream, Encoding encoding, bool insertLineBetweenSections = true, bool withComments = true)
        {
            this.SaveCore(stream, encoding, insertLineBetweenSections, withComments);
        }

        /// <summary>
        /// 核心保存方法
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
        /// 获取INI文本
        /// </summary>
        /// <param name="insertLineBetweenSections">是否在节之间留一个空行</param>
        /// <param name="withComments">是否包含注释</param>
        public string GetText(bool insertLineBetweenSections = true, bool withComments = true)
        {
            if (Sections.Count == 0 && (_comments == null || _comments.Count == 0)) { return string.Empty; }

            //备注
            StringBuilder sb = new StringBuilder();
            if (withComments && _comments != null && _comments.Count != 0)
            {
                foreach (string cmt in _comments)
                {
                    sb.AppendLine(cmt);
                }
            }

            //内容
            foreach (INISection section in this)
            {
                if (insertLineBetweenSections) { sb.AppendLine(); }

                sb.Append(section.GetText(withComments));
            }

            return sb[0] == '\r'
                ? sb.ToString(Environment.NewLine.Length, sb.Length - Environment.NewLine.Length) //去首个空行
                : sb.ToString();
        }

        /// <summary>
        /// 修改节名。原节不存在则什么也不做
        /// </summary>
        public void ChangeSectionName(string currName, string newName)
        {
            if (!INISection.IsWellSectionOrKeyName(newName)) { throw new ArgumentException("新节名无效！"); }

            INISection val;
            if (!TryGetValue(currName, out val)) { return; }

            //允许大小写修改
            if (ContainsKey(newName) && !string.Equals(currName, newName, StringComparison.OrdinalIgnoreCase))
            { throw new ArgumentException("已有相同节名！"); }

            Remove(currName);
            Add(newName, val);
            val.Name = newName;//节名也要改
        }

        /// <summary>
        /// 判断是否包含指定节
        /// </summary>
        public bool Contains(string sectionName)
        {
            return ContainsKey(sectionName);
        }

        /// <summary>
        /// 根据路径设置值。节和键不存在会自动创建
        /// </summary>
        public void Set(string path, string value)
        {
            string section, key;
            ParsePath(path, out section, out key);

            this[section, key] = value;
        }

        /// <summary>
        /// 根据路径设置值（T.ToString）。节和键不存在会自动创建
        /// </summary>
        public void Set<T>(string path, T value)
        {
            Set(path, value == null ? null : value.ToString());
        }

        #region 辅助方法

        /// <summary>
        /// 取包含于某对字符中的文本
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
        /// 解析路径表达式
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
                throw new FormatException("路径表达式无效！");
            }

            if (!INISection.IsWellSectionOrKeyName(sectionAndKey[0]) || !INISection.IsWellSectionOrKeyName(sectionAndKey[1]))
            {
                throw new ArgumentException(string.Format("节点名【{0}】或键名【{1}】无效！", sectionAndKey[0], sectionAndKey[1]));
            }

            section = sectionAndKey[0];
            key = sectionAndKey[1];
        }

        #endregion

        #region 根据表达式直接获取值

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public string Get(string path, string defaultValue = "")
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.Get(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public T Get<T>(string path, T defaultValue = default(T))
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.Get(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public int GetInt32(string path, int defaultValue = 0)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetInt32(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public bool GetBoolean(string path, bool defaultValue = false)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetBoolean(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public DateTime? GetDateTime(string path, DateTime? defaultValue = null)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetDateTime(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public decimal GetDecimal(string path, decimal defaultValue = 0)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetDecimal(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 根据路径表达式获取值
        /// </summary>
        /// <param name="path">Section/Key的形式。注意是正斜杠</param>
        /// <param name="defaultValue">节点或键不存在时返回该值</param>
        public Uri GetUri(string path, Uri defaultValue = null)
        {
            string section, key;
            ParsePath(path, out section, out key);

            INISection s;
            return TryGetValue(section, out s) ? s.GetUri(key, defaultValue) : defaultValue;
        }

        #endregion

        /// <summary>
        /// 添加节。若节已存在则什么也不做
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
        /// 添加节。若节已存在则会被替换
        /// </summary>
        public void Add(INISection section)
        {
            if (section == null) { throw new ArgumentNullException(); }
            base[section.Name] = section;
        }

        /// <summary>
        /// 复制节到指定数组
        /// </summary>
        public void CopyTo(INISection[] array, int arrayIndex)
        {
            this.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 移除指定节
        /// </summary>
        public new bool Remove(string sectionName)
        {
            return base.Remove(sectionName);
        }

        /// <summary>
        /// 移除指定节
        /// </summary>
        public bool Remove(INISection section)
        {
            return section != null && base.Remove(section.Name);
        }

        //替换基类返回KV，本类只返回V
        public new IEnumerator<INISection> GetEnumerator()
        {
            return this.Values.GetEnumerator();
        }

        /// <summary>
        /// 实现序列化
        /// </summary>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("body", this.GetText(false));
        }
    }

    /// <summary>
    /// INI节
    /// </summary>
    public class INISection : Dictionary<string, string>
    {
        StringCollection _comments;
        string _name;

        /// <summary>
        /// 节注释容器
        /// </summary>
        internal StringCollection Comments
        {
            get { return _comments ?? (_comments = new StringCollection()); }
        }

        /// <summary>
        /// 获取节名
        /// </summary>
        public string Name
        {
            get { return _name; }

            //不对外开放
            internal set
            {
                if (!IsWellSectionOrKeyName(value)) { throw new FormatException("节名无效！"); }

                if (value == _name) { return; }

                _name = value;
            }
        }

        /// <summary>
        /// 获取或设置指定键的值。若键不存在则返回null，赋值则自动添加键
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
                if (!IsWellSectionOrKeyName(key)) { throw new ArgumentException("键名无效！"); }
                base[key] = value;
            }
        }

        /// <summary>
        /// 初始化节
        /// </summary>
        /// <param name="name">节名</param>
        public INISection(string name)
            : base(StringComparer.OrdinalIgnoreCase)
        {
            Name = name;
        }

        /// <summary>
        /// 获取指定键的值。若键不存在或值为空时返回defaultValue
        /// </summary>
        public T Get<T>(string key, T defaultValue = default(T))
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? (T)Convert.ChangeType(val, typeof(T))
                : defaultValue;
        }

        /// <summary>
        /// 获取指定键的值。【注意】若键不存在则返回defaultValue，若值为空则返回string.Empty
        /// </summary>
        public string Get(string key, string defaultValue = "")
        {
            string val;
            return TryGetValue(key, out val)
                ? val
                : defaultValue;
        }

        /// <summary>
        /// 获取int。若键不存在或值为空时返回defaultValue
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="defaultValue">当键不存在时返回该值</param>
        /// <returns>值为空时会尝试转换，所以可能会抛转换异常</returns>
        public int GetInt32(string key, int defaultValue = 0)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? int.Parse(val)
                : defaultValue;
        }

        /// <summary>
        /// 获取bool，可处理1/0/true/false。若键不存在或值为空时返回defaultValue
        /// </summary>
        public bool GetBoolean(string key, bool defaultValue = false)
        {
            string val; int valAsInt;
            return TryGetValue(key, out val) && val.Length != 0
                ? (int.TryParse(val, out valAsInt) ? Convert.ToBoolean(valAsInt) : Convert.ToBoolean(val))
                : defaultValue;
        }

        /// <summary>
        /// 获取decimal。若键不存在或值为空时返回defaultValue
        /// </summary>
        public decimal GetDecimal(string key, decimal defaultValue = 0)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? decimal.Parse(val)
                : defaultValue;
        }

        /// <summary>
        /// 获取DateTime?。若键不存在或值为空时返回defaultValue
        /// </summary>
        public DateTime? GetDateTime(string key, DateTime? defaultValue = null)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? DateTime.Parse(val)
                : defaultValue;
        }

        /// <summary>
        /// 获取Uri。若键不存在或值为空时返回defaultValue
        /// </summary>
        public Uri GetUri(string key, Uri defaultValue = null)
        {
            string val;
            return TryGetValue(key, out val) && val.Length != 0
                ? new Uri(val)
                : defaultValue;
        }

        /// <summary>
        /// 添加键值。若键已存在则更新
        /// </summary>
        public new void Add(string key, string value)
        {
            this[key] = value;
        }

        /// <summary>
        /// 获取节文本
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

            //写备注
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
        /// 确定给定字符串是否是合法的节和键名
        /// </summary>
        public static bool IsWellSectionOrKeyName(string name)
        {
            return name != null && name.Trim().Length != 0
                && name.IndexOfAny(new[] { '[', ']', '/' }) == -1;
        }
    }
}