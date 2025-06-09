using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using Services;

namespace MenuzRus {

    public static class Utility {
        #region Public Methods

        public static byte[] ReadImageFile(String imageLocation) {
            byte[] imageData = null;
            FileInfo fileInfo = new FileInfo(imageLocation);
            long imageFileLength = fileInfo.Length;
            FileStream fs = new FileStream(imageLocation, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            imageData = br.ReadBytes((int)imageFileLength);
            return imageData;
        }

        public static string ToJson(this object obj, int recursionDepth = 100) {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.RecursionLimit = recursionDepth;
            return serializer.Serialize(obj);
        }

        public static List<T> ToListObject<T>(this string obj, int recursionDepth = 100) {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.RecursionLimit = recursionDepth;
            List<T> returnList = serializer.Deserialize<List<T>>(obj);
            return returnList;
        }

        public static IEnumerable<SelectListItem> ToSelectListItems<T, R>(this IDictionary<T, R> dic) {
            return dic.Select(x => new SelectListItem() { Text = x.Value.ToString(), Value = x.Key.ToString() });
        }

        #endregion Public Methods
    }
}