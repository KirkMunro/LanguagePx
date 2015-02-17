using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace LanguagePx
{
    class DslDatabase
    {
        static DataSet dslDb = null;

        static DataTable GetKeywordTable()
        {
            if (dslDb == null)
            {
                dslDb = new DataSet("DomainSpecificLanguages");
                DataTable dslTable = dslDb.Tables.Add("Keywords");

                dslTable.PrimaryKey = new DataColumn[] {
                    dslTable.Columns.Add("KeywordId", typeof(int))
                };
                dslTable.Columns.Add("DslName", typeof(string));
                dslTable.Columns.Add("Keyword", typeof(DynamicKeyword));
                dslTable.Columns.Add("Path", typeof(string));
                dslTable.Columns.Add("ParentKeywordId", typeof(int));
                dslTable.Columns.Add("OnInvoking", typeof(ScriptBlock));
                dslTable.Columns.Add("OnInvoked", typeof(ScriptBlock));

                return dslTable;
            }

            return dslDb.Tables["Keywords"];
        }

        static object GetKeywordProperty(int keywordId, string propertyName)
        {
            DataTable dslTable = GetKeywordTable();

            DataRow row = dslTable.Rows.Find(keywordId);

            object dbPropertyValue = row[propertyName];

            return dbPropertyValue is DBNull ? null : dbPropertyValue;
        }

        static object GetKeywordProperty(DynamicKeyword keyword, string propertyName)
        {
            return GetKeywordProperty(keyword.GetHashCode(), propertyName);
        }

        public static void AddKeyword(string dslName, DynamicKeyword keyword, int parentKeywordId, string keywordPath)
        {
            DataTable dslTable = GetKeywordTable();

            DataRow row = dslTable.NewRow();
            row["KeywordId"] = keyword.GetHashCode();
            row["DslName"] = dslName;
            row["Keyword"] = keyword;
            row["Path"] = keywordPath;
            row["ParentKeywordId"] = parentKeywordId;
            row["OnInvoking"] = null;
            row["OnInvoked"] = null;
            dslTable.Rows.Add(row);
        }

        public static List<DynamicKeyword> GetChildKeywords(DynamicKeyword keyword)
        {
            return GetKeywordTable()
                .AsEnumerable()
                .Where(x => (int)x["ParentKeywordId"] == keyword.GetHashCode())
                .Select(x => (DynamicKeyword)x["Keyword"])
                .ToList();
        }

        public static string GetPath(DynamicKeyword keyword)
        {
            return (string)GetKeywordProperty(keyword, "Path");
        }

        public static List<DynamicKeyword> GetDslRootKeywords(string dslName)
        {
            DataTable dslTable = GetKeywordTable();

            return dslTable
                .AsEnumerable()
                .Where(x => ((int)x["ParentKeywordId"] == 0) && (string.Compare((string)x["DslName"], dslName, true) == 0))
                .Select(x => (DynamicKeyword)x["Keyword"])
                .ToList();
        }

        public static DynamicKeyword GetKeyword(string dslName, string keywordPath)
        {
            DataTable dslTable = GetKeywordTable();

            DataRow row = dslTable
                .AsEnumerable()
                .FirstOrDefault(x => (string.Compare((string)x["DslName"], dslName, true) == 0) && (string.Compare((string)x["Path"], keywordPath, true) == 0));

            if (row == null)
            {
                return null;
            }

            return (DynamicKeyword)row["Keyword"];
        }

        static ScriptBlock GetEventHandler(DynamicKeyword keyword, string eventName)
        {
            return (ScriptBlock)GetKeywordProperty(keyword, eventName);
        }

        public static ScriptBlock GetOnInvokingEventHandler(DynamicKeyword keyword)
        {
            return GetEventHandler(keyword, "OnInvoking");
        }

        public static ScriptBlock GetOnInvokedEventHandler(DynamicKeyword keyword)
        {
            return GetEventHandler(keyword, "OnInvoked");
        }

        static void SetEventHandler(string dslName, string keywordPath, string eventName, ScriptBlock eventHandler)
        {
            DynamicKeyword keyword = GetKeyword(dslName, keywordPath);

            if (keyword == null)
            {
                return;
            }

            DataTable dataTable = GetKeywordTable();

            DataRow row = dataTable.Rows.Find(keyword.GetHashCode());
            if (row == null)
            {
                return;
            }

            row[eventName] = eventHandler;
        }

        public static void SetOnInvokedEventHandler(string dslName, string keywordPath, ScriptBlock eventHandler)
        {
            SetEventHandler(dslName, keywordPath, "OnInvoked", eventHandler);
        }

        public static void SetOnInvokingEventHandler(string dslName, string keywordPath, ScriptBlock eventHandler)
        {
            SetEventHandler(dslName, keywordPath, "OnInvoking", eventHandler);
        }
    }
}