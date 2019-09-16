local i : int = 0;
repeat
	local l : string;
	if i < 3 then
	   l = "test " .. i;
	else
	   l = nil;
	end;
	print(l);
	i = i + 1;
until l == nil;
return i;
