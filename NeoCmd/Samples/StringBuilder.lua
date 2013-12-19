local s = "Hallo Welt";
local sb = clr.System.Text.StringBuilder();

sb:Append(cast(string, string.upper(s)));

print(sb:ToString());