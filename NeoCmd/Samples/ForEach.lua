function text()
  return "Hallo", "Welt";
end;

function test(...)
  foreach c in ... do
    print(c);
  end;
end;

foreach c in text() do
  print(c);
end;