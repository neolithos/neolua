local function a()
	local i = 1;
	local t = function()
		i = i + 1;
		return i;
	end;
	return i, t;
end;

local i, b = a();
b();
b();

return i, b();