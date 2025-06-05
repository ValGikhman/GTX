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

        #region Public Fields

        public static readonly IDictionary<String, String> States = new Dictionary<String, String> {
            { "AL", "Alabama" },
            { "AK", "Alaska" },
            { "AZ", "Arizona" },
            { "AR", "Arkansas" },
            { "CA", "California" },
            { "CO", "Colorado" },
            { "CT", "Connecticut" },
            { "DE", "Delaware" },
            { "DC", "District of Columbia" },
            { "FL", "Florida" },
            { "GA", "Georgia" },
            { "HI", "Hawaii" },
            { "ID", "Idaho" },
            { "IL", "Illinois" },
            { "IN", "Indiana" },
            { "IA", "Iowa" },
            { "KS", "Kansas" },
            { "KY", "Kentucky" },
            { "LA", "Louisiana" },
            { "ME", "Maine" },
            { "MD", "Maryland" },
            { "MA", "Massachusetts" },
            { "MI", "Michigan" },
            { "MN", "Minnesota" },
            { "MS", "Mississippi" },
            { "MO", "Missouri" },
            { "MT", "Montana" },
            { "NE", "Nebraska" },
            { "NV", "Nevada" },
            { "NH", "New Hampshire" },
            { "NJ", "New Jersey" },
            { "NM", "New Mexico" },
            { "NY", "New York" },
            { "NC", "North Carolina" },
            { "ND", "North Dakota" },
            { "OH", "Ohio" },
            { "OK", "Oklahoma" },
            { "OR", "Oregon" },
            { "PA", "Pennsylvania" },
            { "RI", "Rhode Island" },
            { "SC", "South Carolina" },
            { "SD", "South Dakota" },
            { "TN", "Tennessee" },
            { "TX", "Texas" },
            { "UT", "Utah" },
            { "VT", "Vermont" },
            { "VA", "Virginia" },
            { "WA", "Washington" },
            { "WV", "West Virginia" },
            { "WI", "Wisconsin" },
            { "WY", "Wyoming" }
        };

        #endregion Public Fields

        #region Public Methods

        public static String GetNewConfirmationNumber() {
            byte[] buffer = Guid.NewGuid().ToByteArray();
            return BitConverter.ToUInt32(buffer, 8).ToString();
        }

        public static byte[] ReadImageFile(String imageLocation) {
            byte[] imageData = null;
            FileInfo fileInfo = new FileInfo(imageLocation);
            long imageFileLength = fileInfo.Length;
            FileStream fs = new FileStream(imageLocation, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            imageData = br.ReadBytes((int)imageFileLength);
            return imageData;
        }

        public static IEnumerable<Decimal> SplitAmount(Decimal value, int count) {
            if (count <= 0) throw new ArgumentException("count must be greater than zero.", "count");
            Decimal[] result = new Decimal[count];

            Decimal runningTotal = 0;
            for (int i = 0; i < count; i++) {
                Decimal remainder = value - runningTotal;
                Decimal share = remainder > 0 ? Math.Max(Math.Round(remainder / (count - i), 2), 1 ^ -2) : 0;
                result[i] = share;
                runningTotal += share;
            }

            if (runningTotal < value) result[count - 1] += value - runningTotal;

            return result.OrderBy(m => m);
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