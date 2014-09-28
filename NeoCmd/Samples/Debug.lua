const string typeof System.String;

function F1(a)
  local b = a;
  a = a + 1;
  print(a, b);
eee();
end;

--[[local function FL()
  print("Local function.");
end;]]

F1(2);
--FL();
