local sum = 0;
for c in 
	function(s, v)
		if v == 0 then
			return 1;
		elseif v == 1 then 
			return 20; 
		elseif v == 20 then
			return 300;
		elseif v == 300 then
			return 4000;
		else
			return nil;
		end;
	end, nil, 0 do

	sum = sum + c;
end;
return sum;