local CompareMeClass = clr.LuaDLR.Test.ControlStructures.CompareMe;

local a = CompareMeClass:ctor(1);
local b = CompareMeClass:ctor(4);
local c = 5;
local d = 0;

if a < b then
	d = d + 1;
end;
if c > a then
	d = d + 1;
end;
if a < c then
	d = d + 1;
end;
if a < 3 then
	d = d + 1;
end;
if 3 > a then
	d = d + 1;
end;
if "a" < "b" then
	d = d + 1;
end;
return d;