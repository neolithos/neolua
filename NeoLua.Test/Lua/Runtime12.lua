local s = "from=world, to=Lua"
local i = 0;
local t = "";
for k, v in string.gmatch(s, "(%w+)=(%w+)") do
	print(k .. "=" .. v);
	i = i + 1;
	t = t .. k .. v;
end;
return i, t;