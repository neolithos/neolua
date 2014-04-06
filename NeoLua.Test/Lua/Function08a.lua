local function a()
	local i : int = 1;
	local t = function() : int
		i = i + 1;
		return i;
	end;
	return i, t;
end;

local i : int, b = a();
b();
b();

return i, b();