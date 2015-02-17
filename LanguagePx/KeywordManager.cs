using System;
using System.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace LanguagePx
{
    class KeywordManager
    {
        static DataSet keywordDb = null;
        static string keywordTableName = "Keywords";
        static string dslDetailsTableName = "DslDetails";
        static Stack<DynamicKeyword> dslKeywordStack = new Stack<DynamicKeyword>();

        public static PowerShellHelper PSHelper { get; set; }

        static void InitializeDb()
        {
            if (keywordDb == null)
            {
                keywordDb = new DataSet("KeywordDatabase");

                DataTable table = keywordDb.Tables.Add(keywordTableName);
                table.PrimaryKey = new DataColumn[] {
                    table.Columns.Add("KeywordId", typeof(int))
                };
                table.Columns.Add("Keyword", typeof(DynamicKeyword));
                table.Columns.Add("Module", typeof(string));
                table.Columns.Add("Visible", typeof(bool));
                table.Columns.Add("OnInvoking", typeof(ScriptBlock));
                table.Columns.Add("OnInvoked", typeof(ScriptBlock));

                table = keywordDb.Tables.Add(dslDetailsTableName);
                table.PrimaryKey = new DataColumn[] {
                    table.Columns.Add("KeywordId", typeof(int))
                };
                table.Columns.Add("DslName", typeof(string));
                table.Columns.Add("Path", typeof(string));
                table.Columns.Add("ParentKeywordId", typeof(int));
            }
        }

        static DataTable GetTable(string name)
        {
            InitializeDb();
            return keywordDb.Tables[name];
        }

        static List<DynamicKeyword> FindAllKeywords()
        {
            return DynamicKeyword.GetKeyword();
        }

        static DynamicKeyword FindKeyword(DynamicKeyword keyword)
        {
            return DynamicKeyword.GetKeyword(keyword.Keyword);
        }

        static void RegisterKeyword(DynamicKeyword keyword)
        {
            if (FindKeyword(keyword) != keyword)
            {
                DynamicKeyword.AddKeyword(keyword);
            }

            Collection<PSObject> results = PSHelper.InvokeCommand(
                "Get-Alias",
                new OrderedDictionary {
                    { "Name", keyword.Keyword },
                    { "Scope", "Script" },
                    { "ErrorAction", ActionPreference.Ignore }
                });
            AliasInfo aliasInfo = null;
            if ((results != null) && (results.Count == 1))
            {
                aliasInfo = results[0].BaseObject as AliasInfo;
            }
            if ((aliasInfo == null) || (string.Compare(aliasInfo.Definition, "Invoke-Keyword", true) != 0))
            {
                PSHelper.InvokeCommand(
                    "Set-Alias",
                    new OrderedDictionary {
                        { "Name", keyword.Keyword },
                        { "Value", "Invoke-Keyword" },
                        { "Scope", "Script" },
                        { "Description", "This alias is automatically managed by the LanguagePx module." },
                        { "Force", true },
                        { "Confirm", false },
                        { "WhatIf", false },
                        { "ErrorAction", ActionPreference.Stop }
                    });
            }
        }

        static void UnregisterKeyword(DynamicKeyword keyword)
        {
            if (FindKeyword(keyword) == keyword)
            {
                DynamicKeyword.RemoveKeyword(keyword.Keyword);
            }

            // Remove all aliases for the current keyword that reference the Invoke-Keyword
            // command (we need to remove multiple if the module author defined the aliases
            // that are automatically defined themselves; otherwise the module will not
            // auto-reload properly once it is unloaded because of the crumbs that are left
            // behind)
            AliasInfo aliasInfo = null;
            do
            {
                aliasInfo = null;
                Collection<PSObject> results = PSHelper.InvokeCommand(
                    "Get-Alias",
                    new OrderedDictionary {
                        { "Name", keyword.Keyword },
                        { "ErrorAction", ActionPreference.Ignore }
                    });
                if ((results != null) && (results.Count == 1))
                {
                    aliasInfo = results[0].BaseObject as AliasInfo;
                }
                if ((aliasInfo != null) && (string.Compare(aliasInfo.Definition, "Invoke-Keyword", true) == 0))
                {
                    PSHelper.InvokeCommand(
                        "Remove-Item",
                        new OrderedDictionary {
                            { "LiteralPath", string.Format("Alias::{0}", keyword.Keyword) },
                            { "Force", true },
                            { "Confirm", false },
                            { "WhatIf", false },
                            { "ErrorAction", ActionPreference.Stop }
                        });
                }
            } while ((aliasInfo != null) && (string.Compare(aliasInfo.Definition, "Invoke-Keyword", true) == 0));
        }

        static void AddKeyword(DynamicKeyword keyword, PSModuleInfo module, bool visible)
        {
            DataTable table = GetTable(keywordTableName);
            DataRow row = table.NewRow();
            row["KeywordId"] = keyword.GetHashCode();
            row["Keyword"] = keyword;
            row["Module"] = module == null ? null : module.Name;
            row["Visible"] = visible;
            row["OnInvoking"] = null;
            row["OnInvoked"] = null;
            table.Rows.Add(row);

            if (visible)
            {
                RegisterKeyword(keyword);
            }
        }

        public static void AddStandaloneKeyword(DynamicKeyword keyword, PSModuleInfo module)
        {
            AddKeyword(keyword, module, true);
        }

        public static void AddDslKeyword(string dslName, DynamicKeyword keyword, int parentKeywordId, string keywordPath, PSModuleInfo module)
        {
            AddKeyword(keyword, module, dslKeywordStack.Count == 0 && parentKeywordId == 0);

            DataTable table = GetTable(dslDetailsTableName);
            DataRow row = table.NewRow();
            row["KeywordId"] = keyword.GetHashCode();
            row["DslName"] = dslName;
            row["Path"] = keywordPath;
            row["ParentKeywordId"] = parentKeywordId;
            table.Rows.Add(row);
        }

        public static List<DynamicKeyword> GetStandaloneKeywords()
        {
            DataTable dslDetailsTable = GetTable(dslDetailsTableName);
            return GetTable(keywordTableName)
                .AsEnumerable()
                .Where(x => dslDetailsTable.Rows.Find((int)x["KeywordId"]) == null)
                .Select(x => (DynamicKeyword)x["Keyword"])
                .ToList();
        }

        public static DynamicKeyword GetStandaloneKeyword(string name)
        {
            return GetStandaloneKeywords()
                .FirstOrDefault(x => string.Compare(x.Keyword, name, true) == 0);
        }

        public static List<DynamicKeyword> GetDslKeywords(string dslName)
        {
            return GetTable(dslDetailsTableName)
                .AsEnumerable()
                .Where(x => string.Compare((string)x["DslName"], dslName, true) == 0)
                .Select(x => GetKeyword((int)x["KeywordId"]))
                .ToList();
        }

        public static DynamicKeyword GetDslKeyword(string dslName, string keywordPath)
        {
            DataTable table = GetTable(dslDetailsTableName);

            DataRow row = table
                .AsEnumerable()
                .FirstOrDefault(x => (string.Compare((string)x["DslName"], dslName, true) == 0) && (string.Compare((string)x["Path"], keywordPath, true) == 0));

            if (row == null)
            {
                return null;
            }

            return GetKeyword((int)row["KeywordId"]);
        }

        public static List<DynamicKeyword> GetDslRootKeywords(string dslName = null)
        {
            return GetTable(dslDetailsTableName)
                .AsEnumerable()
                .Where(x => ((int)x["ParentKeywordId"] == 0) && (dslName == null ? true : string.Compare((string)x["DslName"], dslName, true) == 0))
                .Select(x => GetKeyword((int)x["KeywordId"]))
                .ToList();
        }

        public static DynamicKeyword GetDslRootKeyword(string name)
        {
            DataRow row = GetTable(dslDetailsTableName)
                .AsEnumerable()
                .FirstOrDefault(x => ((int)x["ParentKeywordId"] == 0) && (string.Compare(((DynamicKeyword)x["Keyword"]).Keyword, name, true) == 0));

            if (row == null)
            {
                return null;
            }

            return GetKeyword((int)row["KeywordId"]);
        }

        public static void RemoveKeyword(DynamicKeyword keyword)
        {
            DataTable dslDetailsTable = GetTable(dslDetailsTableName);
            DataTable keywordsTable = GetTable(keywordTableName);
            DataRow row = dslDetailsTable.Rows.Find(keyword.GetHashCode());
            if (row != null)
            {
                dslDetailsTable.Rows.Remove(row);
            }
            row = keywordsTable.Rows.Find(keyword.GetHashCode());
            if (row != null)
            {
                keywordsTable.Rows.Remove(row);
            }
            UnregisterKeyword(keyword);
        }

        public static void RemoveDsl(string name)
        {
            foreach (DynamicKeyword keyword in GetDslKeywords(name))
            {
                RemoveKeyword(keyword);
            }
        }

        public static void RemoveStandaloneKeyword(string name)
        {
            DynamicKeyword keyword = GetStandaloneKeyword(name);
            if (keyword != null)
            {
                RemoveKeyword(keyword);
            }
        }

        static object GetKeywordProperty(string tableName, int keywordId, string propertyName)
        {
            DataTable table = GetTable(tableName);

            DataRow row = table.Rows.Find(keywordId);
            if (row == null)
            {
                throw new RowNotInTableException(string.Format("The keyword with id {0} was not found in the keyword database.", keywordId));
            }

            object propertyValue = row[propertyName];

            return propertyValue is DBNull ? null : propertyValue;
        }

        static object GetKeywordProperty(string tableName, DynamicKeyword keyword, string propertyName)
        {
            return GetKeywordProperty(tableName, keyword.GetHashCode(), propertyName);
        }

        static DynamicKeyword GetKeyword(int keywordId)
        {
            return (DynamicKeyword)GetKeywordProperty(keywordTableName, keywordId, "Keyword");
        }

        static string GetModule(DynamicKeyword keyword)
        {
            return (string)GetKeywordProperty(keywordTableName, keyword, "Module");
        }

        public static bool IsVisible(DynamicKeyword keyword)
        {
            return (bool)GetKeywordProperty(keywordTableName, keyword, "Visible");
        }

        public static ScriptBlock GetOnInvokingEventHandler(DynamicKeyword keyword)
        {
            return (ScriptBlock)GetKeywordProperty(keywordTableName, keyword, "OnInvoking");
        }

        public static ScriptBlock GetOnInvokedEventHandler(DynamicKeyword keyword)
        {
            return (ScriptBlock)GetKeywordProperty(keywordTableName, keyword, "OnInvoked");
        }

        public static string GetDslName(DynamicKeyword keyword)
        {
            return (string)GetKeywordProperty(dslDetailsTableName, keyword, "DslName");
        }

        public static string GetPath(DynamicKeyword keyword)
        {
            return (string)GetKeywordProperty(dslDetailsTableName, keyword, "Path");
        }

        public static int GetParentId(DynamicKeyword keyword)
        {
            return (int)GetKeywordProperty(dslDetailsTableName, keyword, "ParentKeywordId");
        }

        public static DynamicKeyword GetParentKeyword(DynamicKeyword keyword)
        {
            return GetKeyword(GetParentId(keyword));
        }

        public static List<DynamicKeyword> GetChildKeywords(DynamicKeyword keyword)
        {
            return GetTable(dslDetailsTableName)
                .AsEnumerable()
                .Where(x => (int)x["ParentKeywordId"] == keyword.GetHashCode())
                .Select(x => GetKeyword((int)x["KeywordId"]))
                .ToList();
        }

        public static List<DynamicKeyword> GetSiblingKeywords(DynamicKeyword keyword)
        {
            int parentKeywordId = GetParentId(keyword);
            return GetTable(dslDetailsTableName)
                .AsEnumerable()
                .Where(x => (int)x["ParentKeywordId"] == parentKeywordId)
                .Select(x => GetKeyword((int)x["KeywordId"]))
                .ToList();
        }

        public static bool IsDslRootKeyword(DynamicKeyword keyword)
        {
            DataRow row = GetTable(dslDetailsTableName).Rows.Find(keyword.GetHashCode());
            if (row == null)
            {
                return false;
            }

            return (int)row["ParentKeywordId"] == 0;
        }

        public static bool IsDslKeyword(DynamicKeyword keyword)
        {
            return GetTable(dslDetailsTableName).Rows.Find(keyword.GetHashCode()) != null;
        }

        public static bool IsStandaloneKeyword(DynamicKeyword keyword)
        {
            return !IsDslKeyword(keyword);
        }

        public static void PushDslKeyword(DynamicKeyword keyword)
        {
            // Throw if the keyword being pushed is standalone
            if (IsStandaloneKeyword(keyword))
            {
                throw new InvalidOperationException(string.Format("Invalid operation: pushing a standalone keyword ({0}) onto the dynamic keyword stack. This should never happen.", keyword.Keyword));
            }

            // Push the DSL keyword onto the stack
            dslKeywordStack.Push(keyword);

            // Hide all DSL keywords that are siblings to the current keyword
            foreach (DynamicKeyword siblingKeyword in KeywordManager.GetSiblingKeywords(keyword))
            {
                HideKeyword(siblingKeyword);
            }

            // Show all DSL child keywords
            foreach (DynamicKeyword childKeyword in KeywordManager.GetChildKeywords(keyword))
            {
                ShowKeyword(childKeyword);
            }
        }

        public static void PopDslKeyword()
        {
            // Throw if there are no DSL keywords on the stack
            if (dslKeywordStack.Count == 0)
            {
                throw new InvalidOperationException("Invalid operation: popping a DSL keyword off of dynamic keyword stack when the dynamic keyword stack is empty. This should never happen.");
            }

            // Pop the DSL keyword off of the stack
            DynamicKeyword keyword = dslKeywordStack.Pop();

            // Hide all DSL child keywords
            foreach (DynamicKeyword childKeyword in GetChildKeywords(keyword))
            {
                HideKeyword(childKeyword);
            }

            // Show all DSL keywords that are siblings to the current keyword
            foreach (DynamicKeyword siblingKeyword in GetSiblingKeywords(keyword))
            {
                ShowKeyword(siblingKeyword);
            }

            // Re-register any standalone keywords that do not have a conflicting keyword name already registered
            foreach (DynamicKeyword standaloneKeyword in GetStandaloneKeywords())
            {
                if (FindKeyword(standaloneKeyword) == null)
                {
                    RegisterKeyword(standaloneKeyword);
                }
            }
        }

        public static bool InsideDsl()
        {
            return dslKeywordStack.Count > 0;
        }

        public static string GetCurrentDslName()
        {
            if (!InsideDsl())
            {
                return null;
            }

            return GetDslName(dslKeywordStack.Peek());
        }

        public static bool IsChildKeyword(DynamicKeyword keyword)
        {
            if (IsStandaloneKeyword(keyword))
            {
                return false;
            }

            if (!InsideDsl())
            {
                return false;
            }

            int parentId = GetParentId(keyword);

            return parentId == dslKeywordStack.Peek().GetHashCode();
        }

        public static List<DynamicKeyword> GetVisibleKeywords(string name = null)
        {
            return GetTable(keywordTableName)
                .AsEnumerable()
                .Where(x => (bool)x["Visible"] && (name == null ? true : string.Compare(((DynamicKeyword)x["Keyword"]).Keyword, name, true) == 0))
                .Select(x => (DynamicKeyword)x["Keyword"])
                .ToList();
        }

        public static DynamicKeyword LoadVisibleKeyword(string name)
        {
            List<DynamicKeyword> visibleKeywords = GetVisibleKeywords(name);
            if (visibleKeywords.Count == 0)
            {
                throw new InvalidOperationException(string.Format("Unable to find a keyword with name '{0}' in the current scope.", name));
            }

            if (visibleKeywords.Count > 1)
            {
                foreach (DynamicKeyword visibleKeyword in visibleKeywords)
                {
                    if (IsDslKeyword(visibleKeyword))
                    {
                        RegisterKeyword(visibleKeyword);
                        return visibleKeyword;
                    }
                }

                throw new InvalidPowerShellStateException(string.Format("Invalid keyword state: multiple standalone keywords with the name '{0}' detected. This should never happen.", name));
            }

            RegisterKeyword(visibleKeywords[0]);
            return visibleKeywords[0];
        }

        static void SetKeywordProperty(DynamicKeyword keyword, string propertyName, object value)
        {
            DataTable table = GetTable(keywordTableName);
            DataRow row = table.Rows.Find(keyword.GetHashCode());
            if (row == null)
            {
                throw new RowNotInTableException(string.Format("The keyword with id {0} was not found in the keyword database.", keyword.GetHashCode()));
            }

            row[propertyName] = value;
        }

        public static void ShowKeyword(DynamicKeyword keyword)
        {
            SetKeywordProperty(keyword, "Visible", true);
            RegisterKeyword(keyword);
        }

        public static void HideKeyword(DynamicKeyword keyword)
        {
            if (IsStandaloneKeyword(keyword))
            {
                throw new InvalidOperationException(string.Format("Invalid operation: attempt to hide standalone keyword '{0}'.", keyword.Keyword));
            }

            SetKeywordProperty(keyword, "Visible", false);
            UnregisterKeyword(keyword);
        }

        static void SetDslKeywordEventHandler(string dslName, string keywordPath, string eventName, ScriptBlock eventHandler)
        {
            DynamicKeyword keyword = GetDslKeyword(dslName, keywordPath);
            if (keyword == null)
            {
                throw new RowNotInTableException(string.Format("The keyword with id {0} was not found in the keyword database.", keyword.GetHashCode()));
            }

            SetKeywordProperty(keyword, eventName, eventHandler);
        }

        public static void SetDslKeywordOnInvokedEventHandler(string dslName, string keywordPath, ScriptBlock eventHandler)
        {
            SetDslKeywordEventHandler(dslName, keywordPath, "OnInvoked", eventHandler);
        }

        public static void SetDslKeywordOnInvokingEventHandler(string dslName, string keywordPath, ScriptBlock eventHandler)
        {
            SetDslKeywordEventHandler(dslName, keywordPath, "OnInvoking", eventHandler);
        }
    }
}