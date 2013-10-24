local sum = 0;
local t = { 1, 20, 300, 4000 }
for i,v in ipairs(t) do
	sum = sum + v;
end;
return sum;