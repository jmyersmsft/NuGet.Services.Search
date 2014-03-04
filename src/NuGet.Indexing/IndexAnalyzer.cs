﻿using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace NuGet.Indexing
{
    public static class IndexAnalyzer
    {
        public static string Analyze(PackageSearcherManager searcherManager)
        {
            if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
            {
                searcherManager.MaybeReopen();
            }

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                JObject report = new JObject();

                report.Add("TotalMemory", GC.GetTotalMemory(false));

                report.Add("NumDocs", indexReader.NumDocs());

                if (indexReader.CommitUserData != null)
                {
                    JObject commitUserdata = new JObject();
                    foreach (KeyValuePair<string, string> userData in indexReader.CommitUserData)
                    {
                        commitUserdata.Add(userData.Key, userData.Value);
                    }
                    report.Add("CommitUserData", commitUserdata);
                }

                JArray segments = new JArray();
                foreach (ReadOnlySegmentReader segmentReader in indexReader.GetSequentialSubReaders())
                {
                    JObject segmentInfo = new JObject();
                    segmentInfo.Add("segment", segmentReader.SegmentName);
                    segmentInfo.Add("documents", segmentReader.NumDocs());
                    segments.Add(segmentInfo);
                }
                report.Add("Segments", segments);

                return report.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        public static string GetDistinctStoredFieldNames(PackageSearcherManager searcherManager)
        {
            if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
            {
                searcherManager.MaybeReopen();
            }

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;

                HashSet<string> distinctFieldNames = new HashSet<string>();

                for (int i = 0; i < indexReader.MaxDoc; i++)
                {
                    if (!indexReader.IsDeleted(i))
                    {
                        Document document = indexReader.Document(i);
                        IList<IFieldable> fields = document.GetFields();
                        foreach (IFieldable field in fields)
                        {
                            distinctFieldNames.Add(field.Name);
                        }
                    }
                }

                JArray array = new JArray();
                foreach (string fieldName in distinctFieldNames)
                {
                    array.Add(fieldName);
                }

                return array.ToString();
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }

        // Doesn't return JSON because consumers will want to make monitoring decisions based on this data as well as saving it/returning it from APIs
        public static IndexConsistencyReport GetIndexConsistency(PackageSearcherManager searcherManager, int databasePackageCount)
        {
            if ((DateTime.UtcNow - searcherManager.WarmTimeStampUtc) > TimeSpan.FromMinutes(1))
            {
                searcherManager.MaybeReopen();
            }

            IndexSearcher searcher = searcherManager.Get();

            try
            {
                IndexReader indexReader = searcher.IndexReader;
                
                // Get the number of documents
                int numDocs = indexReader.NumDocs();

                // Build the report
                return new IndexConsistencyReport(numDocs, databasePackageCount);
            }
            finally
            {
                searcherManager.Release(searcher);
            }
        }
    }
}