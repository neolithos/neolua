local l = {
  add = 100,

  a = function(a)
    return a * 4;
  end,

  c = function(self, a)
    return self.add + a * 2;
  end
};

function l:b(a)
  return self.add + a * 3
end;

function test(a)
  return a * 2;
end;

b = l;
