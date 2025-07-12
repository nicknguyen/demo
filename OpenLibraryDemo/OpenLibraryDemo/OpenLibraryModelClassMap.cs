using CsvHelper.Configuration;

namespace OpenLibraryDemo;

public sealed class OpenLibraryModelClassMap : ClassMap<OpenLibraryDataSetRecord>
{
    public OpenLibraryModelClassMap()
    {
        Map(m => m.WorkKey).Index(0);
        Map(m => m.EditionKey).Index(1).Optional();
        Map(m => m.Rating).Index(2);
        Map(m => m.Date).Index(3);
    }
}