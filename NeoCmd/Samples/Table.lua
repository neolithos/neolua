

function test()
    local t = {};

    table.insert(t, "a");
    table.insert(t, "b");
    table.insert(t, "c");
    table.insert(t, "d");

    print("Table Length: " .. #t);

    return t;
end;

local t = test();

print("Table LengthR: " .. #t);