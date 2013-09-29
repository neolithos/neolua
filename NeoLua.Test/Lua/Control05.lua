local a = 0;
::start::

if a < 10 then
	a = a + 1;
else
	goto ende;
end;
goto start;
::ende::
return a;