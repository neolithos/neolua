const Application typeof System.Windows.Forms.Application;
const Form typeof System.Windows.Forms.Form;
const Button typeof System.Windows.Forms.Button;
const MessageBox typeof System.Windows.Forms.MessageBox;
const MessageBoxButtons typeof System.Windows.Forms.MessageBoxButtons;
const MessageBoxIcon typeof System.Windows.Forms.MessageBoxIcon;
const String typeof System.String;
const Brushes typeof System.Drawing.Brushes;

local iClicked : int = 0;

Application:EnableVisualStyles();

do (frm : Form, cmd : Button = Form(), Button())
	frm.Text = 'Hallo Welt!';
	cmd.Text = 'Click';
	cmd.Left = 16;
	cmd.Top = 16;
	cmd.Click:add(
		function (sender, e) : void
		  iClicked = iClicked + 1;
		  MessageBox:Show(frm, String:Format('Clicked {0:N0} times!', iClicked), 'Lua', MessageBoxButtons.OK, MessageBoxIcon.Information);
		end);
	frm.Paint:add(
	    function (sender, e) : void
		  e.Graphics:FillRectangle(Brushes.Lime, 10, 10, 100, 100);
		end);
	frm.Controls:Add(cmd);
	Application:Run(frm);
end;