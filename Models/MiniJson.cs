using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TextureGrade.Models
{
    /// <summary>
    /// 极简 JSON 读写 —— 只处理本项目用到的「扁平对象：string -> number」这一种结构
    /// （预设就是 { "Exposure": 0.5, "Contrast": -12, ... }）。
    ///
    /// 为什么不用 DataContractJsonSerializer / JavaScriptSerializer：
    /// 二者都需要额外程序集引用，而本项目刻意保持「零 NuGet、零额外引用」，
    /// 加上数据形状极简单，手写几十行比引依赖更省心、也更稳。
    /// </summary>
    public static class MiniJson
    {
        public static string WriteObject(IDictionary<string, double> data)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in data)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kv.Key)).Append("\":")
                  .Append(kv.Value.ToString("R", CultureInfo.InvariantCulture));
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static Dictionary<string, double> ReadObject(string text)
        {
            var result = new Dictionary<string, double>();
            if (string.IsNullOrEmpty(text)) return result;

            int i = 0;
            SkipWs(text, ref i);
            if (i >= text.Length || text[i] != '{') return result;
            i++;

            while (i < text.Length)
            {
                SkipWs(text, ref i);
                if (i >= text.Length) break;
                if (text[i] == '}') break;
                if (text[i] == ',') { i++; continue; }
                if (text[i] != '"') break;              // 结构不符合预期 -> 停止解析

                string key = ReadString(text, ref i);
                SkipWs(text, ref i);
                if (i >= text.Length || text[i] != ':') break;
                i++;
                SkipWs(text, ref i);

                if (!TryReadNumber(text, ref i, out double val)) break;   // 值不是数字就放弃
                if (key != null) result[key] = val;
            }
            return result;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= s.Length &&
                            ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber,
                                            CultureInfo.InvariantCulture, out var code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        private static bool TryReadNumber(string s, ref int i, out double value)
        {
            value = 0;
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                    || s[i] == '-' || s[i] == '+'))
                i++;
            if (i == start) return false;
            return double.TryParse(s.Substring(start, i - start), NumberStyles.Float,
                                   CultureInfo.InvariantCulture, out value);
        }

        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
