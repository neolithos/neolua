local sum = 0;
local t = { 1, 20, 300, 4000 }
foreach c in t do
	sum = sum + c.Value;
end;
return sum;