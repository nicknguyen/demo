using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenLibraryDemo;

public class AuthorsQueryResponse
{
    public string name { get; set; }
    public Created created { get; set; }
    public int[] photos { get; set; }
    public Last_Modified last_modified { get; set; }
    public int latest_revision { get; set; }
    public string key { get; set; }
    public Type type { get; set; }
    public int revision { get; set; }
}