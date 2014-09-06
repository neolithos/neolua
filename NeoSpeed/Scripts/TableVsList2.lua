
local t = clr.System.Collections.Generic.List[clr.System.Int32]();
for i = 1,10000,1 do
	t:Add(i);
end;

local sum = 0;
for i = 0,10000-1,1 do
	sum = sum + t[i];
end;