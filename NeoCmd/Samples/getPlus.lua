const StringBuilder typeof System.Text.StringBuilder;
const String typeof System.String;

function getPlus(lines : int) : String;
	local sb = StringBuilder();
	for i = 1,lines,2 do
	  sb:AppendLine(String('+', i));
	end;
	return sb:ToString();
end;
