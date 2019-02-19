using sozluk_backend.Core.Cache;
using sozluk_backend.Core.Sys.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sozluk_backend.Core.Sys.DataStore
{

    [Serializable]
    class QueryResultBase
    {
        public List<Entry> Entries;
        public int TotalRecordCount;
        public int LogicalRecordCount;
        public int PhysicalRecordCount;


        public bool HasEntry
        {
            get
            {
                if (this.Entries == null)
                    return false;

                return this.Entries.Count > 0;
            }
        }
    }

    [Serializable]
    class SearchAndIndexQueryResult : QueryResultBase
    {
        public string PagerHash;

        public SearchAndIndexQueryResult()
        {
            this.Entries = new List<Entry>();
        }
    }

    [Serializable]
    class ViewQueryResult : QueryResultBase
    {
        public int BaslikId;

        public ViewQueryResult()
        {
            this.Entries = new List<Entry>();
        }
    }

    [Serializable]
    class BaslikBasicInfo
    {
        public long TotalEntries;
        //additional info defs here.
    }

    internal enum RecordPerPageType
    {
        Entries,
        Basliks
    }

    internal class SozlukDataStore
    {
        static readonly TimeSpan EntriesOfBaslikTimeout = TimeSpan.FromMinutes(30);
        static readonly TimeSpan TodaysTimeout = TimeSpan.FromSeconds(30);
        static readonly TimeSpan AllBasliksTimeout = TimeSpan.FromMinutes(5);
        static readonly TimeSpan IndexedBasliksTimeout = TimeSpan.FromMinutes(15);
        static readonly TimeSpan SearchResultTimeout = TimeSpan.FromMinutes(5);
        static readonly TimeSpan SuserInfoTimeout = TimeSpan.MaxValue;

        private static object lockObj;

        internal static int RecordsPerPage
        {
            get
            {
                return Config.Get().RecordCountPerPage;
            }
        }

        internal static int BasliksPerPage
        {
            get
            {
                return Config.Get().BaslikCountPerPage;
            }
        }

        internal static int CalcPageCount(long totalEntries, RecordPerPageType type)
        {
            int recordPerPage = 0;

            switch (type)
            {
                case RecordPerPageType.Basliks:
                    recordPerPage = BasliksPerPage;
                    break;
                case RecordPerPageType.Entries:
                    recordPerPage = RecordsPerPage;
                    break;
                default:
                    return 1;
            }

            int pageCount = (int)(totalEntries / recordPerPage) + 1;

            if (pageCount > 1 && totalEntries % recordPerPage == 0)
            {
                pageCount--;
            }

            return pageCount;
        }

        private static readonly string ADD_SUSER_SQL =
            "IF NOT EXISTS (SELECT Suser FROM Susers WHERE Suser = '{0}') " +
            "BEGIN INSERT INTO Susers(Suser,Password,DummyMail) VALUES('{0}','{1}','{2}') END";

        private static readonly string GET_SUSER_SQL =
            "SELECT Suser, Password FROM Susers WHERE Suser = '{0}';";

        private static readonly string NEW_ENTRY_SQL =
            "DECLARE @BaslikIdent AS INT; " +
            "DECLARE @NewBaslikInsert AS INT = 0; " +
            "SELECT @BaslikIdent = Id FROM Basliks WHERE Baslik = '{0}'; " +
            "IF @BaslikIdent IS NULL " +
            "BEGIN " +
            "INSERT INTO Basliks(Baslik) VALUES('{0}'); " +
            "SET @BaslikIdent = @@IDENTITY; " +
            "SET @NewBaslikInsert = 1;" +
            "END " +
            "INSERT INTO Entries (BaslikId, SuserId, Date,Descr) " +
            "VALUES(@BaslikIdent, (SELECT Id FROM Susers WHERE Suser = '{1}'), '{2}', '{3}'); " +
            "SELECT @BaslikIdent As BaslikId, @NewBaslikInsert As IsNewBaslikInsert;";
        
        private static readonly string SEARCH_SUSER_ID_GET_SQL =
            "DECLARE @SuserIdent INT = 0;" +
            "SELECT @SuserIdent = Id FROM Susers WHERE Suser = '{0}';";


        private static readonly string SEARCH_COND_COUNT_CONTENT =
            "CASE WHEN Baslik LIKE '%{0}%' AND Descr NOT LIKE '%{0}%' THEN NULL ELSE Baslik END";

        private static readonly string SEARCH_COND_COUNT_ALL = "Baslik";

        private static readonly string SEARCH_SQL_ALL_BASE =
            "SELECT * FROM (" +
            "SELECT ROW_NUMBER() OVER(Order By Basliks.Baslik) As RowNum, " +
            "Basliks.Baslik, " +
            "COUNT(Entries.BaslikId) As EntryCount, " +
            "COUNT(*) OVER() As TotalRecordCount " +
            "FROM Entries INNER JOIN Basliks ON Basliks.Id = Entries.BaslikId %%CONDITION%% GROUP BY Basliks.Baslik " +
            ") X WHERE %%ROW_LIMIT_CONDITION%%";

        private static readonly string GET_ENTRIES_OF_BASLIK_SQL_BASE =
            "SELECT * FROM(" +
            "SELECT " +
            "ROW_NUMBER() OVER(Order By Basliks.Id) as RowNum," +
            "Basliks.Id, " + 
            "Basliks.Baslik," +
            "Susers.Suser," +
            "Entries.Descr As[Entry]," +
            "Entries.Date As SubmitDate, " +
            "COUNT(*) OVER() AS TotalRecord " +
            "FROM Entries " +
            "INNER JOIN Basliks ON Basliks.Id = Entries.BaslikId " +
            "INNER JOIN Susers ON Susers.Id = Entries.SuserId " +
            "WHERE %%BASLIK_SEARCH_CONDITION%% " +
            ") X WHERE %%ROW_LIMIT_CONDITION%%;";

        private static readonly string SEARCH_SQL_BASE =
            "SELECT * FROM ( " +
            "SELECT " +
            "ROW_NUMBER() OVER(Order By Baslik) as RowNum, Baslik," +
            "COUNT( %%COUNT_CONDITION%% ) As EntryCount," +
            "COUNT(*) OVER() As TotalRecordCount " +
            "FROM " +
            "(SELECT Entries.Id, Basliks.Baslik, Susers.Suser, Entries.Descr,Entries.Date FROM Basliks " +
            "INNER JOIN Entries ON Basliks.Id = Entries.BaslikId " +
            "INNER JOIN Susers ON Susers.Id = Entries.SuserId " +
            "%%CONDITIONS%%" +
            ") ConditionSubQuery GROUP BY Baslik " +
            ") FilterQuery WHERE %%ROW_LIMIT_CONDITION%%";

        private static readonly string SEARCH_COND_SUSER =
            "(Entries.SuserId = @SuserIdent)";

        private static readonly string SEARCH_COND_CONTENT =
            "(Basliks.Baslik LIKE '%{0}%' OR Entries.Descr LIKE '%{0}%')";

        private static readonly string SEARCH_COND_DATE =
            "(Entries.Date BETWEEN '{0}' AND '{1}')";

        static SozlukDataStore()
        {
            lockObj = new object();
        }

        private static void GetTodaysDateRange(out DateTime todayBegin, out DateTime justNow)
        {
            DateTime now = DateTime.Now;
            todayBegin = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);
            justNow = new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,now.Second <= 30 ? 0 : 30);
        }

        private static string BuildFetchAllSQLQuery(ref string baseQueryHash, int rowBegin, int rowEnd, char indexChar)
        {
            string query;

            if (!CacheManager.TryGetCachedResult<string>(baseQueryHash, out query))
            {
                query = SEARCH_SQL_ALL_BASE;

                if ((indexChar >= 'a' && indexChar <= 'z'))
                {
                    query = query.Replace("%%CONDITION%%",
                        string.Format(" WHERE (LEFT(Baslik,1) = '{0}')", indexChar));
                }
                else if (indexChar == '*')
                {
                    query = query.Replace("%%CONDITION%%",
                        string.Format(" WHERE (LEFT(Baslik,1) >= '0' AND LEFT(Baslik,1) <= '9')"));
                }
                else if (indexChar == '.')
                {
                    query = query.Replace("%%CONDITION%%", "");
                }

                
                baseQueryHash = Helper.Md5(query);

                CacheManager.CacheObject(baseQueryHash, query);
            }

            query = query.Replace("%%ROW_LIMIT_CONDITION%%", 
                string.Format("(RowNum BETWEEN {0} AND {1})", rowBegin, rowEnd));

            return query;
        }

        private static string BuildFetchSQLQuery(
            ref string baseQueryHash,
            string content, 
            string suser, 
            DateTime begin, DateTime end,
            int rowBegin,int rowEnd)
        {
            bool linkAnd = false;
            string query;

            StringBuilder sb = new StringBuilder();
            StringBuilder cond = new StringBuilder();

            if (!CacheManager.TryGetCachedResult<string>(baseQueryHash, out query))
            {

                if (!string.IsNullOrEmpty(suser))
                    sb.AppendFormat(SEARCH_SUSER_ID_GET_SQL, suser.Trim());

                if (!string.IsNullOrEmpty(content))
                {
                    cond.AppendFormat(SEARCH_COND_CONTENT, content.Trim());
                    linkAnd = true;
                }

                if (!string.IsNullOrEmpty(suser))
                {
                    if (linkAnd)
                        cond.Append(" AND ");
                    else
                        linkAnd = true;

                    cond.Append(SEARCH_COND_SUSER);
                }

                if (begin != DateTime.MinValue && end != DateTime.MinValue)
                {
                    if (linkAnd)
                        cond.Append(" AND ");

                    cond.AppendFormat(SEARCH_COND_DATE, begin.ToString(), end.ToString());

                }

                sb.Append(SEARCH_SQL_BASE);

                if (cond.Length > 0)
                {
                    cond.Insert(0, "WHERE ");
                    sb.Replace("%%CONDITIONS%%", cond.ToString());
                }
                else
                {
                    sb.Replace("%%CONDITIONS%%", string.Empty);
                }

                if (!string.IsNullOrEmpty(content))
                    sb.Replace("%%COUNT_CONDITION%%", string.Format(SEARCH_COND_COUNT_CONTENT, content));
                else
                    sb.Replace("%%COUNT_CONDITION%%", SEARCH_COND_COUNT_ALL);


                if (!string.IsNullOrEmpty(suser))
                    sb.Append(" AND EntryCount > 0");

                sb.Append(";");

                baseQueryHash = Helper.Md5(sb.ToString());

                CacheManager.CacheObject(baseQueryHash, sb.ToString());
            }
            else
            {
                sb.Append(query);
            }

            sb.Replace("%%ROW_LIMIT_CONDITION%%",
                string.Format("RowNum BETWEEN {0} AND {1}", rowBegin, rowEnd));
            
            query = sb.ToString();

            cond.Clear();
            sb.Clear();

            cond = null;
            sb = null;

            return query;
        }

        public static bool AddSuser(Suser suser, out bool registered)
        {
            bool result;

            registered = false;

            if (GetSuser(suser.SuserName) != null)
            {
                return true;
            }

            SqlServerIo sql = SqlServerIo.Create();

            if (!sql.Ready)
                return false;

            result = sql.Execute(
                true, 
                ADD_SUSER_SQL, 
                suser.SuserName, suser.PasswordHash, "dummy@dummy.org"
                );

            if (result)
            {
                registered = sql.RecordCount > 0;
            }


            SqlServerIo.Release(sql);

            return result;
        }

        public static bool AddEntry(Entry entry)
        {
            bool result;

            if (string.IsNullOrEmpty(entry.Content))
                return false;

            SqlServerIo sql = SqlServerIo.Create();

            if (!sql.Ready)
                return false;

            //naa
            entry.FixForMultipleLineFeeds();

            result = sql.Execute(false, NEW_ENTRY_SQL, entry.Baslik,entry.Suser,entry.Date.ToString(),entry.Content);

            if (result)
            {
                
                if (!sql.Read())
                    result = false;
                else
                {
                    entry.SetId(sql.GetValueOfColumn<int>("BaslikId"));

                    
                    CacheManager.InvalidateCacheSet(KeysetId.Baslik(entry.BaslikID));

                    var indexKeyid = KeysetId.Index(entry.Baslik[0]);
                    CacheManager.InvalidateCacheSet(indexKeyid);
                    
                    if (sql.GetValueOfColumn<int>("IsNewBaslikInsert") == 1)
                    {
                        //invalidate also todays section keyset a.k.a Taze
                        CacheManager.InvalidateCacheSet(KeysetId.Todays());
                    }
                }
                
            }


            SqlServerIo.Release(sql);

            return result;
        }

        public static Suser GetSuser(string suser)
        {

            string query = string.Format(GET_SUSER_SQL, suser);
            Suser suserObject = null;

            if (!CacheManager.TryGetCachedQueryResult<Suser>(query,out suserObject))
            {
                SqlServerIo sql = SqlServerIo.Create();
                
                if (sql.Execute(false, query))
                {
                    if (sql.Read())
                    {
                        suserObject = new Suser(
                            0,
                            sql.GetValueOfColumn<string>("Suser"),
                            sql.GetValueOfColumn<string>("Password")
                            );

                    }
                    
                }

                SqlServerIo.Release(sql);
                
            }
            
            return suserObject;
        }

        private static SearchAndIndexQueryResult FetchBasliksIndexed(int pageNumber, char beginChar, string pagerHash)
        {
            SearchAndIndexQueryResult resultSet;
            SqlServerIo sql;
            TimeSpan invTimeout;
            int rowBegin, rowEnd;
            string query, baslik;
            int entryCount;

            rowBegin = (pageNumber * BasliksPerPage) + 1;
            rowEnd = rowBegin + BasliksPerPage - 1;

            beginChar = char.ToLower(beginChar);

            if (beginChar == '.')
                invTimeout = AllBasliksTimeout;
            else
                invTimeout = IndexedBasliksTimeout;
            
            query = BuildFetchAllSQLQuery(ref pagerHash, rowBegin, rowEnd,beginChar);
            
            if (!CacheManager.TryGetCachedQueryResult<SearchAndIndexQueryResult>(query, out resultSet))
            {
                sql = SqlServerIo.Create();

                if (!sql.Execute(false, query))
                {
                    SqlServerIo.Release(sql);
                    return new SearchAndIndexQueryResult();
                }

                resultSet = new SearchAndIndexQueryResult
                {
                    PagerHash = pagerHash
                };

                while (sql.Read())
                {
                    baslik = sql.GetValueOfColumn<string>("Baslik");
                    entryCount = sql.GetValueOfColumn<int>("EntryCount");

                    if (resultSet.TotalRecordCount == 0)
                        resultSet.TotalRecordCount = sql.GetValueOfColumn<int>("TotalRecordCount");

                    resultSet.Entries.Add(new Entry(baslik, string.Empty,string.Empty, string.Empty, entryCount));

                    resultSet.PhysicalRecordCount++;
                }

                resultSet.LogicalRecordCount = resultSet.Entries.Count;

                SqlServerIo.Release(sql);
                
                CacheManager.CacheObject(KeysetId.Index(beginChar,true),true, query, resultSet,invTimeout);
            }

            return resultSet;
        }



        private static string Strev(string s)
        {
            string ns = "";

            for (int i = s.Length - 1; i >= 0; i--)
                ns += s[i];

            return ns;
        }

        public static SearchAndIndexQueryResult FetchBasliksUsingSearch(bool fresh, string content, string suser, DateTime begin, DateTime end, int pageNumber, string pagerHash, bool leaveDatesAsIs)
        {
            SearchAndIndexQueryResult resultSet;
            SqlServerIo sql;
            TimeSpan invTimeout;
            int rowBegin, rowEnd;
            string query;
            KeysetId keysetId;
            string baslik, descr, deceptedDate;
            int entryCount;
            bool resultCached;

            rowBegin = (pageNumber * BasliksPerPage) + 1;
            rowEnd = rowBegin + BasliksPerPage - 1;

            //Workarounds, workarounds, workarounds !
            if (begin != DateTime.MinValue && end != DateTime.MinValue)
            {
                if (leaveDatesAsIs)
                {
                    deceptedDate = begin.AddTicks((end - begin).Ticks / 2).ToString();
                }
                else
                {
                    //push it out of from the search date range to reverse daterange check logic
                    deceptedDate = end.AddDays(2).ToString();
                }
            }
            else
                deceptedDate = string.Empty;

            query = BuildFetchSQLQuery(ref pagerHash, content, suser, begin, end, rowBegin, rowEnd);


            if (fresh)
            {
                keysetId = KeysetId.Todays(true);
                invTimeout = TodaysTimeout;
            }
            else
            {
                keysetId = KeysetId.Search(pagerHash,true);
                invTimeout = SearchResultTimeout;
            }

            resultCached = CacheManager.TryGetCachedQueryResult<SearchAndIndexQueryResult>(query, out resultSet);

            if (!resultCached)
            {
                sql = SqlServerIo.Create();

                if (!sql.Execute(false, query))
                {
                    SqlServerIo.Release(sql);
                    return new SearchAndIndexQueryResult();
                }

                resultSet = new SearchAndIndexQueryResult
                {
                    PagerHash = pagerHash
                };

                while (sql.Read())
                {
                    baslik = sql.GetValueOfColumn<string>("Baslik");
                    entryCount = sql.GetValueOfColumn<int>("EntryCount");

                    if (resultSet.TotalRecordCount == 0)
                        resultSet.TotalRecordCount = sql.GetValueOfColumn<int>("TotalRecordCount");

                    if (entryCount>0)
                        descr = content;
                    else
                        descr = Strev(content);

                    if (string.IsNullOrEmpty(suser))
                        suser = string.Empty;

                    resultSet.Entries.Add(
                        new Entry(
                            baslik, 
                            suser, 
                            deceptedDate, 
                            descr,entryCount)
                            );

                    resultSet.PhysicalRecordCount++;
                }

                resultSet.LogicalRecordCount = resultSet.Entries.Count;

                SqlServerIo.Release(sql);

                CacheManager.CacheObject(keysetId,true, query, resultSet,invTimeout);
            }

            return resultSet;
        }

        public static SearchAndIndexQueryResult FetchBasliks(string index, int pageNumber, string pagerHash)
        {
            DateTime begin, end;
            
            //fresh contents
            if (string.IsNullOrEmpty(index))
            {
                GetTodaysDateRange(out begin, out end);

                return FetchBasliksUsingSearch(true,string.Empty, string.Empty, begin, end, pageNumber,pagerHash,true);
            }

            index = index.ToLower();

            if (index == "all")
                return FetchBasliksIndexed(pageNumber,'.',pagerHash);

            if (index == "*")
                return FetchBasliksIndexed(pageNumber, '*',pagerHash);

            if (index[0] >= 'a' && index[0] <= 'z')
                return FetchBasliksIndexed(pageNumber, index[0],pagerHash);

            return new SearchAndIndexQueryResult();
        }

        private static string BuildEntryFetchSQL(string baslik, int baslikId, int pageNumber)
        {
            int rowBegin, rowEnd;
            string query, searchCondition;
            rowBegin = (pageNumber * RecordsPerPage) + 1;
            rowEnd = rowBegin + RecordsPerPage - 1;

            if (baslikId > 0)
                searchCondition = "Basliks.Id = " + baslikId.ToString();
            else
                searchCondition = string.Format("Basliks.Baslik = '{0}'", baslik);

            query = GET_ENTRIES_OF_BASLIK_SQL_BASE.Replace("%%BASLIK_SEARCH_CONDITION%%", searchCondition);


            query = query.Replace("%%ROW_LIMIT_CONDITION%%",
                string.Format("RowNum BETWEEN {0} AND {1}", rowBegin, rowEnd));

            return query;
        }

        public static ViewQueryResult FetchEntriesOfBaslik(string baslik, int baslikId, int pageNumber)
        {
            SqlServerIo sql;
            string query;
            ViewQueryResult resultSet;

            query = BuildEntryFetchSQL(baslik, baslikId, pageNumber);

            
            if (!CacheManager.TryGetCachedQueryResult<ViewQueryResult>(query, out resultSet))
            {
                sql = SqlServerIo.Create();

                if (!sql.Execute(false, query))
                {
                    SqlServerIo.Release(sql);
                    return new ViewQueryResult();
                }

                resultSet = new ViewQueryResult();

                while (sql.Read())
                {
                    if (resultSet.TotalRecordCount == 0)
                    {
                        resultSet.TotalRecordCount = sql.GetValueOfColumn<int>("TotalRecord");
                        resultSet.BaslikId = sql.GetValueOfColumn<int>("Id");
                    }

                    Entry e = new Entry(
                        sql.GetValueOfColumn<string>("Baslik"),
                        sql.GetValueOfColumn<string>("Suser"),
                        sql.GetValueOfColumn<DateTime>("SubmitDate").ToString(),
                        sql.GetValueOfColumn<string>("Entry"));

                    resultSet.Entries.Add(e);
                    resultSet.PhysicalRecordCount++;

                }

                SqlServerIo.Release(sql);

                //Dont cache for empty recordset for baslik
                if (!resultSet.HasEntry)
                    return resultSet;

                resultSet.LogicalRecordCount = resultSet.PhysicalRecordCount;

                CacheManager.CacheObject(KeysetId.Baslik(resultSet.BaslikId), true, query, resultSet);

                BaslikBasicInfo bbi = new BaslikBasicInfo()
                {
                    TotalEntries = resultSet.TotalRecordCount
                };

                CacheManager.CacheObject("BBI_" + resultSet.BaslikId.ToString(),
                    bbi,
                    TimeSpan.FromMinutes(10));


                //if this request initial fetch using the baslik string.
                //rebuild sql with baslikid and cache the result with its hashkey
                if (baslikId == 0)
                {
                    query = BuildEntryFetchSQL(null, resultSet.BaslikId, pageNumber);
                    CacheManager.CacheObject(KeysetId.Baslik(resultSet.BaslikId),true, query, resultSet);
                }
            }

            return resultSet;
        }

    }

}
