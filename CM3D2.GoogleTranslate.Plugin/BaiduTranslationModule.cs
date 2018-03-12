using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityEngine;
using System.Net;

namespace CM3D2.AutoTranslate.Plugin
{
	internal class BaiduTranslationModule : TranslationModule
	{
		public override string Section => "Baidu";
		private string _targetLanguage = "zh";
        private string _appId = "0";
        private string _appSecret = "0";

		public override bool Init()
		{
			// Do Nothing
			StartCoroutine(Test());
			return true;
		}

		private IEnumerator Test()
		{
			var dat = new TranslationData()
			{
				Id = 0,
				ProcessedText = "Hallo Welt"
			};
			var cd = CreateCoroutineEx(TranslateBaidu(dat.ProcessedText, "de", "en", dat));
			yield return cd.coroutine;
			try
			{
				cd.Check();
			    if (dat.State == TranslationState.Finished)
			    {
			        Logger.Log("Baidu seems OK", Level.Debug);
			    }
			    else
			    {
			        Logger.Log("There seems to be a problem with Baidu!", Level.Warn);
			    }
            }
			catch (Exception e)
			{
			    Logger.Log("There seems to be a problem with Baidu!", Level.Warn);
                Logger.Log(e);
			}
		}

		protected override void LoadConfig(CoreUtil.SectionLoader section)
        {
            section.LoadValue("TargetLanguage", ref _targetLanguage);
            section.LoadValue("AppId", ref _appId);
            section.LoadValue("AppSecret", ref _appSecret);
        }

		public override IEnumerator Translate(TranslationData data)
		{
			var cd = CreateCoroutineEx(TranslateBaidu(data.ProcessedText, "jp", _targetLanguage, data));
			yield return cd.coroutine;
			if (cd.GetException() != null)
			{
				Logger.LogException(cd.GetException(), Level.Warn);
				data.State = TranslationState.Failed;
			}
		}

		public override void DeInit()
		{
			// Do Nothing
		}
        
        // From https://stackoverflow.com/questions/11454004/calculate-a-md5-hash-from-a-string
        public static string CreateMD5(string input)
        {
            // Use input string to calculate MD5 hash
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                return sb.ToString().ToLower(); // Baidu only likes lowercase md5 for no reason
            }
        }

        private IEnumerator TranslateBaidu(string text, string fromCulture, string toCulture, TranslationData translation)
		{
			fromCulture = fromCulture.ToLower();
			toCulture = toCulture.ToLower();

			translation.ProcessedText = text;
			translation.State = TranslationState.Failed;
            
            // From http://blog.csdn.net/u014571132/article/details/53334930
            string salt = System.DateTime.Now.Millisecond.ToString();

            string md5 = CreateMD5(_appId + text + salt + _appSecret);

            string url = String.Format("http://api.fanyi.baidu.com/api/trans/vip/translate?q={0}&from={1}&to={2}&appid={3}&salt={4}&sign={5}",
                WWW.EscapeURL(text), fromCulture, toCulture, _appId, salt, md5);
            var headers = new Dictionary<string, string> { { "User-Agent", "Mozilla/5.0" }, { "Accept-Charset", "UTF-8" } };
            var www = new WWW(url, null, headers);
            yield return www;

            if (www.error != null)
            {
                Logger.LogError(www.error);
                yield break;
            }
            
            string result = string.Empty;

            var jsonResult = JsonFx.Json.JsonReader.Deserialize<TranslationResult>(www.text);
            Logger.Log(www.text);
            for (int i = 0; i < jsonResult.trans_result.Length; i++) {
                // Only add non-japanese chars to prevent repeated translation
                foreach(char ch in jsonResult.trans_result[i].dst) {
                    if(!AutoTranslatePlugin.is_japanese_char(ch)) {
                        result += ch;
                    }
                }
            }
            result = result.Replace("\\n", "");

            Logger.Log($"Got Translation from Baidu: {result}", Level.Debug);

			translation.Translation = result;
			translation.State = TranslationState.Finished;
		}

        public class Translation
        {
            public string src { get; set; }
            public string dst { get; set; }
        }

        public class TranslationResult
        {
            public string error_code { get; set; }
            public string error_msg { get; set; }
            public string from { get; set; }
            public string to { get; set; }
            public string query { get; set; }
            public Translation[] trans_result { get; set; }
        }
    }
}
