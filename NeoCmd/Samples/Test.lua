local e = function ()
	error("hallo");
end;

--e();
local a, b = tonumber(read("a")), tonumber(read("b"));

local PrintResult = function (o, op)

	print(o .. ' = ' .. a .. op .. b);

end;

PrintResult(a + b, " + ");
PrintResult(a - b, " - ");
PrintResult(a * b, " * ");
PrintResult(a / b, " / ");
PrintResult(a // b, " // ");
