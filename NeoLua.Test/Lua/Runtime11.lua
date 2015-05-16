local s = "hello world from Lua 6"
local i = 0;
local t = "";
for w in string.gmatch(s, "%a+") do
	print(w);
	i = i + 1;
	t = t .. w;
end;
return i, t;