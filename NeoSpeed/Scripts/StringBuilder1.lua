StringBuilder = luanet.import_type("System.Text.StringBuilder");

local sb = StringBuilder();

for i = 0,1000,1 do
	sb:Append(".");
end;

return sb:ToString();