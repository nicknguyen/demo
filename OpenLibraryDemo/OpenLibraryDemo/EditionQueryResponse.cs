using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenLibraryDemo;

public class EditionQueryResponse
{
    public string[] publishers { get; set; }
    public string weight { get; set; }
    public int[] covers { get; set; }
    public string physical_format { get; set; }
    public Last_Modified last_modified { get; set; }
    public int latest_revision { get; set; }
    public string key { get; set; }
    public Author1[] authors { get; set; }
    public string ocaid { get; set; }
    public string[] subjects { get; set; }
    public string[] isbn_13 { get; set; }
    public Classifications classifications { get; set; }
    public string title { get; set; }
    public Identifiers identifiers { get; set; }
    public Created created { get; set; }
    public Language[] languages { get; set; }
    public string[] isbn_10 { get; set; }
    public string publish_date { get; set; }
    public Work[] works { get; set; }
    public Type type { get; set; }
    public string physical_dimensions { get; set; }
    public int revision { get; set; }
}

public class Classifications
{
}

public class Identifiers
{
    public string[] goodreads { get; set; }
    public string[] librarything { get; set; }
}

public class Language
{
    public string key { get; set; }
}

public class Work
{
    public string key { get; set; }
}