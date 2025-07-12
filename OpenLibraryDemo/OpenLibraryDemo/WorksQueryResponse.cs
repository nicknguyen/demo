namespace OpenLibraryDemo;

public class WorksQueryResponse
{
    public Description description { get; set; }
    public string title { get; set; }
    public Created created { get; set; }
    public Last_Modified last_modified { get; set; }
    public int latest_revision { get; set; }
    public string key { get; set; }
    public Author[] authors { get; set; }
    public Type type { get; set; }
    public int revision { get; set; }
}

public class Description
{
    public string type { get; set; }
    public string value { get; set; }
}

public class Created
{
    public string type { get; set; }
    public DateTime value { get; set; }
}

public class Last_Modified
{
    public string type { get; set; }
    public DateTime value { get; set; }
}

public class Type
{
    public string key { get; set; }
}

public class Author
{
    public Type1 type { get; set; }
    public Author1 author { get; set; }
}

public class Type1
{
    public string key { get; set; }
}

public class Author1
{
    public string key { get; set; }
}