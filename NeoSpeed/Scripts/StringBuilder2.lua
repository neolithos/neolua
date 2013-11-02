local sb = clr.System.Text.StringBuilder:ctor();

for i = 0,1000,1 do
	sb:Append(".");
end;

return sb:ToString();