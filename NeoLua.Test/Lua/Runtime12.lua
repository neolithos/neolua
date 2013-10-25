local s = "from=world, to=Lua"
local i = 0;
for k, v in string.gmatch(s, "(%w+)=(%w+)") do
	print(k .. "=" .. v);
	i = i + 1;
end;
return i;