smoutput=: (150!:0)&'Console.WriteLine(v.ToString());return new MicroJ.A<MicroJ.JString>(0);'

NB. fib test
fibtestx =: 3 : 0
  x1 =. (3!:101) 1 NB. convert to bignum
  x2 =. (3!:101) 1
  for_c. i. (y-2) do. NB. !hotspot
    tmp =.  x1 
    x1 =. x2
    x2 =. tmp + x1 NB. type:BigInteger
  end.
  x2
)

NB. requires LumenWorks.Framework.IO.dll
NB. https://www.nuget.org/packages/LumenWorksCsvReader/    
readcsvCode =: 0 : 0
//css_ref LumenWorks.Framework.IO.dll
//css_ref System.Data
//css_using MicroJ    
//css_using System.IO
//css_using System.Linq
//css_using System.Collections.Generic    
//css_using LumenWorks.Framework.IO.Csv
//css_using System.Text.RegularExpressions
    
var y = v as MicroJ.A<Box>;
string fileName = ((A<JString>)y.Ravel[0].val).Ravel[0].str;
string tableName = ((A<JString>)y.Ravel[1].val).Ravel[0].str;

var newNames = new List<string>();

var iLine = 0;
var types = new int[255];
string[] headers = null;
int fieldCount = 0;
using (var csv = new CsvReader(new StreamReader(fileName), true)) {
    headers = csv.GetFieldHeaders();
    fieldCount = csv.FieldCount;
    var total = 0;
    while (csv.ReadNextRecord() && iLine++ < 10000)
    {
        for(var k = 0; k < fieldCount;k++)
        {
            int n = 0;
            double d = 0;
            if ((types[k] == 0 || types[k] == 3) && Int32.TryParse(csv[k], out n)) {
                if (csv[k].StartsWith("0")) {
                    types[k] = 1;
                } else {
                    types[k] = 3;
                }
            }
            else if ((types[k] == 0 || types[k] == 2) && Double.TryParse(csv[k], out d))
            {
                types[k] = 2;
            }
            else {
                types[k] = 1;
            }
        }
    }
    /*
    for (int i = 0; i < fieldCount; i++) {
        Console.WriteLine(string.Format("{0} = {1}; Type={2}",
                                        headers[i], csv[i], types[i]));
    }
    */
}
using (var csv = new CsvReader(new StreamReader(fileName), true)) {
    csv.Columns = new List<LumenWorks.Framework.IO.Csv.Column>();
    for(var k = 0; k < fieldCount; k++) {
        Type t = null;
        if (types[k] == 2) { t = typeof(long); }
        else if (types[k] == 3) { t = typeof(double); }
        else { t = typeof(string); }
        csv.Columns.Add(new Column { Name = headers[k], Type = t });
    }
    csv.ReadNextRecord();

    var strings = new Dictionary<string, List<string>>();
    var longs = new Dictionary<string, List<long>>();
    var doubles = new Dictionary<string, List<double>>();

    var rowCount = 0;
    while (csv.ReadNextRecord()) {
        rowCount++;
	for (int i = 0; i < csv.FieldCount; i++) {
	    var columnType = types[i];
            var columnName = headers[i];

	    if (columnType == 3) {
		if (!longs.ContainsKey(columnName)) {
		    longs[columnName] = new List<long>();
		}
		long lv = (long) Int64.Parse(csv[i]);
		longs[columnName].Add(lv);
	    }
	    else if (columnType == 1) {
		if (!strings.ContainsKey(columnName)) {
		    strings[columnName] = new List<string>();
		}
		var sv = String.Intern(csv[i]);
		strings[columnName].Add(sv);
	    }
	    else if (columnType == 3) {
		if (!doubles.ContainsKey(columnName)) {
		    doubles[columnName] = new List<double>();
		}
		double dv = Double.Parse(csv[i]);
		doubles[columnName].Add(dv);

	    }
            /*
	    else if (columnType == "System.DateTime") {
		if (!strings.ContainsKey(columnName)) {
		    strings[columnName] = new List<string>();
		}
		DateTime dv = (DateTime) (reader.IsDBNull(i) ? DateTime.MinValue : reader.GetDateTime(i));
		strings[columnName].Add(dv.ToString("yyyy-MM-dd"));
	    }
            */
        }
    }

    Func<string, string> createName = s => {
         return tableName + "_" + Regex.Replace(s, @"[^A-Za-z]+", "");
    };

    
    foreach(var col in longs.Keys) {
        var jname = new MicroJ.A<long>(rowCount) { Ravel = longs[col].ToArray() };
        var newName = createName(col);
        newNames.Add(newName);
        parser.Names[newName] = jname;
    }
    foreach(var col in doubles.Keys) {
        var jname = new MicroJ.A<double>(rowCount) { Ravel = doubles[col].ToArray() };
        var newName = createName(col);
        newNames.Add(newName);
        parser.Names[newName] = jname;
    }
    foreach(var col in strings.Keys) {
        var max = strings[col].Select(x=>x.Length).Max();
        var jname = new MicroJ.A<JString>(rowCount, new long[] { rowCount, max }) { Ravel = strings[col].Select(x=>new MicroJ.JString { str = x }).ToArray() };
        var newName = createName(col);
        newNames.Add(newName);
        parser.Names[newName] = jname;
    }
    /*
    foreach(var newName in newNames) {
        Console.WriteLine(newName);
    }
    */
}

return new MicroJ.A<MicroJ.JString>(new long[] { newNames.Count, 100 }) { Ravel = newNames.Select(x=>new MicroJ.JString { str = x }).ToArray() };
)


NB. reads a csv file delimited by comma and writes names to global namespace for each column prefixed by table parameter
NB. auto-detects type from first 1000 rows   
NB. parameter: box of filename;tablename
NB. example: readcsv 'abc.csv';'abc'
readcsv  =: (150!:0) & readcsvCode

