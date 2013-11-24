local lst = clr.System.Collections.Generic.List[clr.System.Object]();

lst:Add(1);
lst:Add(2);
lst:Add("String");

print("Enum:");
foreach a in lst do
  print(a);
end;

print("Index:");
for i = 0,lst.Count-1,1 do
  print(i .. ": " .. lst[i]);
end;

return lst.Count;