
local t = {};
for i = 1,10000,1 do
	t[i] = i;
end;

local sum = 0;
for i = 1,10000,1 do
	sum = sum + t[i];
end;