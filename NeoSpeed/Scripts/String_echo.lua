
local sum = 0;

for i = 0, 1000, 1 do
	sum = sum .. echo(".");
end;

return sum;