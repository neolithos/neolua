
local t = {};
-- C-Lua schneller
for i = 1,10000,1 do
	t[i] = i;
end;

-- interessant! C-Lua ziemlich lahm
for i = 1,100,1 do
	table.remove(t, i + 1000);
end;
for i = 1,100,1 do
	table.insert(t, i + 1000, 1);
end;

-- C-Lua schneller
local sum = 0;
for i = 1,10000,1 do
	sum = sum + t[i];
end;