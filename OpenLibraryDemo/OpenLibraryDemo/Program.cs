// See https://aka.ms/new-console-template for more information

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using CommandLine;
using CsvHelper;
using CsvHelper.Configuration;
using Flurl.Http;
using MoreLinq;
using OpenLibraryDemo;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .Build();

var openLibDataSetFile = configuration["appSettings:OpenLibDataSetFile"];
var openLibBaseUri = configuration["appSettings:OpenLibBaseUri"];

// Load openlibrary dataset into strongly-typed records
// note: ideally, this would be imported into a rdbms given it's size / record count.
var openLibDsFilepath = Path.Combine(AppContext.BaseDirectory, openLibDataSetFile);
using var reader = new StreamReader(openLibDsFilepath);
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = false,
    Delimiter = "\t"
};
using var tsv = new CsvReader(reader, config);
tsv.Context.RegisterClassMap<OpenLibraryModelClassMap>();
var dataSetRecords = tsv.GetRecords<OpenLibraryDataSetRecord>();
const int bucketSize = 500;
var batch = dataSetRecords.Batch(bucketSize);

await Parser.Default.ParseArguments<Options>(args)
    .WithParsedAsync(async opts =>
    {
        var workDateCounts = new ConcurrentDictionary<DateTime, int>();
        var records = new ConcurrentBag<(string workKey, string editionKey)>();
        switch (opts)
        {
            case { Date: null, Rating: null, WorkKey: null }:
            {
                Console.WriteLine("No arguments specified. Getting the total number of works by date...");
                Parallel.ForEach(batch, (recordChunk, token) =>
                {
                    var groupByDate = GroupByDate(recordChunk, opts, rec => true);
                    LoadWorkDateCounts(groupByDate, workDateCounts);
                });

                foreach (var rec in workDateCounts.ToImmutableSortedDictionary())
                {
                    Console.WriteLine($"{rec.Key.ToShortDateString()}: {rec.Value} work(s).");
                }

                break;
            }
            case { Date: null, Rating: not null, WorkKey: null }:
            {
                Console.WriteLine($"Rating: {opts.Rating} specified. Getting the total number of works by rating...");
                Parallel.ForEach(batch, (recordChunk, token) =>
                {
                    var groupByDate = GroupByDate(recordChunk, opts, rec => rec.Rating == opts.Rating.Value);
                    LoadWorkDateCounts(groupByDate, workDateCounts);
                });
                if (!workDateCounts.IsEmpty)
                {
                    foreach (var rec in workDateCounts.ToImmutableSortedDictionary())
                    {
                        Console.WriteLine(
                            $"Rating: {opts.Rating} -> {rec.Key.ToShortDateString()}: {rec.Value} work(s).");
                    }
                }
                else
                {
                    Console.WriteLine($"No work(s) found for rating: {opts.Rating}.");
                }

                break;
            }
            case { Date: not null, Rating: not null, WorkKey: null }:
            {
                Console.WriteLine(
                    $"Date: {opts.Date.Value.ToShortDateString()} & Rating: {opts.Rating} specified. Getting the work(s) title...");

                Parallel.ForEach(batch,
                    (recordChunk, _) =>
                    {
                        QueryDsAndLoadWorkEditionKeys(recordChunk, records, record => record.Rating == opts.Rating.Value && record.Date == opts.Date.Value);
                    });
                if (!records.IsEmpty)
                {
                    foreach (var rec in records)
                    {
                        var title = (await GetJsonDataForOut(rec)).title;

                        Console.WriteLine($"WorkKey: {rec.workKey}, EditionKey: {rec.editionKey}, Title: {title}");

                        // pause to throttle api call frequency.
                        await Task.Delay(250);
                    }
                }
                else
                {
                    Console.WriteLine($"No work(s) found for rating: {opts.Rating} and date: {opts.Date}.");
                }

                break;
            }
            case { WorkKey: not null }:
            {
                Console.WriteLine($"work key: {opts.WorkKey} specified. Processing...");
                Parallel.ForEach(batch,
                    (recordChunk, token) =>
                    {
                        QueryDsAndLoadWorkEditionKeys(recordChunk, records, record => string.Equals(record.WorkKey, opts.WorkKey, StringComparison.OrdinalIgnoreCase));
                    });
                if (!records.IsEmpty)
                {
                    var jsonDataList = new List<WorkDataInfo>();
                    foreach (var rec in records)
                    {
                        var data = await GetJsonDataForOut(rec, true);
                        jsonDataList.Add(new WorkDataInfo()
                            { authorName = data.author, subject = data.subject, title = data.title });
                        // Console.WriteLine($"WorkKey: {rec.workKey}, EditionKey: {rec.editionKey}");
                    }

                    string fileName = $"{opts.WorkKey.Replace(@"/works/", "")}.json";
                    string filePath = Path.Combine(AppContext.BaseDirectory, fileName);
                    await using FileStream createStream = File.Create(filePath);
                    await JsonSerializer.SerializeAsync(createStream, jsonDataList);
                    Console.WriteLine($"{fileName} saved.");
                }
                else
                {
                    Console.WriteLine($"Work not found for work key: {opts.WorkKey}.");
                }

                break;
            }
        }
    });

async Task<(string title, string subject, string author)> GetJsonDataForOut(
    (string workKey, string editionKey) valueTuple, bool fetchAuthor = false)
{
    var title1 = string.Empty;
    var subject = string.Empty;
    var author = string.Empty;
    if (!string.IsNullOrWhiteSpace(valueTuple.editionKey))
    {
        try
        {
            var editionQueryResponse = await ($"{openLibBaseUri}{valueTuple.editionKey}.json")
                .GetJsonAsync<EditionQueryResponse>();
            title1 = editionQueryResponse?.title;
            subject = editionQueryResponse?.subjects?.FirstOrDefault();
            author = await GetAuthorAsync(fetchAuthor, editionQuery: editionQueryResponse);
        }
        catch (FlurlHttpException ex)
        {
            // todo: add logging
            Console.WriteLine($"Error returned from {ex.Call.Request.Url}: {ex.Message}");
        }
    }
    else
    {
        try
        {
            var worksQueryResponse = await ($"{openLibBaseUri}{valueTuple.workKey}.json")
                .GetJsonAsync<WorksQueryResponse>();
            title1 = worksQueryResponse?.title;
            author = await GetAuthorAsync(fetchAuthor, worksQuery: worksQueryResponse);
        }
        catch (FlurlHttpException ex)
        {
            // todo: add logging
            Console.WriteLine($"Error returned from {ex.Call.Request.Url}: {ex.Message}");
        }
    }

    async Task<string> GetAuthorAsync(bool getAuthor, EditionQueryResponse editionQuery = null,
        WorksQueryResponse worksQuery = null)
    {
        const string authorNotAvail = "N/A";
        if (!getAuthor) return authorNotAvail;

        // todo: need to rework this due to the edition author type vs works author type when deserializing.
        var authorKey = editionQuery == null ? worksQuery?.authors?.FirstOrDefault()?.author?.key : editionQuery?.authors?.FirstOrDefault()?.key;

        return (!string.IsNullOrWhiteSpace(authorKey)
            ? (await ($"{openLibBaseUri}{authorKey}.json").GetJsonAsync<AuthorsQueryResponse>())?.name
            : authorNotAvail) ?? string.Empty;
    }

    return (title1, subject, author);
}

void QueryDsAndLoadWorkEditionKeys(IEnumerable<OpenLibraryDataSetRecord> openLibraryDataSetRecords, ConcurrentBag<(string workKey, string editionKey)> concurrentBag,
    Func<OpenLibraryDataSetRecord, bool> predicate)
{
    var workEditionKeys = openLibraryDataSetRecords.Where(predicate)
        .Select(record => (record.WorkKey, record.EditionKey));
    foreach (var workEditionKey in workEditionKeys)
    {
        concurrentBag.Add(workEditionKey);
    }
}

IEnumerable<(DateTime Date, int WorkCount)> GroupByDate(IEnumerable<OpenLibraryDataSetRecord> openLibraryDataSetRecords,
    Options options, Func<OpenLibraryDataSetRecord, bool> predicate)
{
    return openLibraryDataSetRecords.Where(predicate)
        .GroupBy(rec => rec.Date)
        .Select(g => (Date: g.Key, WorkCount: g.Count()));
}

void LoadWorkDateCounts(IEnumerable<(DateTime Date, int WorkCount)> dateWorkCount,
    ConcurrentDictionary<DateTime, int> concurrentDictionary)
{
    foreach (var work in dateWorkCount)
    {
        if (concurrentDictionary.TryAdd(work.Date, work.WorkCount))
        {
        }
        else
        {
            if (concurrentDictionary.TryGetValue(work.Date, out var workCount))
            {
                concurrentDictionary.TryUpdate(work.Date, workCount + work.WorkCount, workCount);
            }
        }
    }
}