-- Load Forms
clr.System.Reflection.Assembly:Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

local Forms = clr.System.Windows.Forms;
local iClicked : int = 0;

Forms.Application:EnableVisualStyles();

do (frm, cmd = Forms.Form(), Forms.Button())
	frm.Text = 'Hallo Welt!';
	cmd.Text = 'Click';
	cmd.Left = 16;
	cmd.Top = 16;
	cmd.Click:add(
		function (sender, e) : void
		  iClicked = iClicked + 1;
		  Forms.MessageBox:Show(frm, clr.System.String:Format('Clicked {0:N0} times!', iClicked), 'Lua', Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
		end);
	frm.Controls:Add(cmd);
	Forms.Application:Run(frm);
end;