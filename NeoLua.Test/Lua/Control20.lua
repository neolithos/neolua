local t = {};
local j = 1;
for i = 500000500000, 500000500001 do
	print(tostring(i));
	t[j] = i; 
	j = j + 1;
end;
return t[1], t[2];