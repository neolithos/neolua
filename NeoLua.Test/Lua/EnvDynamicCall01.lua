local l = {
  a = function(a)
    return a * 4;
  end
};

function l:b(a)
  return a * 3
end;

function test(a)
  return a * 2;
end;

b = l;