local s = "hello world from Lua 6"
local i = 0;
for w in string.gmatch(s, "%a+") do
	print(w);
	i = i + 1;
end;
return i;