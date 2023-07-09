X2Shell


X2Shell is an application development framework, allowing for simple or complex applications to be built out of simple XML configurations.

The core of X2Shell is the concept of the Data Cloud.  When any X2Shell application begins, it immediately creates a living XML document, 

which is loaded with the base environment information, to be made available to any commands that the application needs to perform.

All of that data is available via standard XPaths.  

For instance, to retrieve the time that the application started, a command needs to simply refer to /X2Shell/run-time/@systemtime

The command-line paramaters are found at: /X2Shell/run-time/command-line/arguments/arg

The machine name that this application is running on is at: /X2Shell/run-time/system/@machine-name

The same information can be queried directly from the Environment Variables, but using the following XPath:

	/X2Shell/run-time/environment/env-var[@name='COMPUTERNAME']


The real power of X2Shell is that EVERY command given to the X2Shell will ALSO be found in the Data Cloud, as soon as it executes.

For instance, the HelloWorld application only has a few commands, starting with <echo> and <pause>, and then OPTIONALLY, <dump-log>

depending on how the <pause> command responds.


		<echo>Hello World!</echo>

		<pause>
			<params method="msgbox" caption="View Data Cloud?" text="Would you lke to view the data cloud?" buttons="ok-cancel"/>
		</pause>
		
		<dump-log test="/X2Shell/pause/@dialog-result='ok'" />



This is how those same command show up in the data cloud:


  <echo id="" timestamp="1/6/2023 9:57:27 AM" new-line="true" text="Hello World!" start-time="1/6/2023 9:57:27 AM" write-to-console="true" write-to-file="false" end-time="1/6/2023 9:57:27 AM" />

  <pause id="" timestamp="1/6/2023 9:57:27 AM" method="msgbox" start-time="1/6/2023 9:57:27 AM" dialog-result="ok" end-time="1/6/2023 9:57:28 AM" />

  <dump-log id="" timestamp="1/6/2023 9:57:28 AM" filepath="" start-time="1/6/2023 9:57:28 AM" include-external-data="false" />


Notice how the pause command was given a method parameter of "msgbox", which we see again in the data cloud, but ALSO an attribute of dialog-result="ok".

The pause command gives us a dialog-result for msgbox based pause commands, based on the button that was clicked.

Other commands will bring the data in as one or more child nodes of the command itself.  For instance, <command> <http> and <sql> commands can all 

generate a substantial amount of data, as well as <load-file>, which can bring in even more data *OR* more commands.

Remember that the command <run-batch> can be pointed to any XPath within the Data Cloud.  This means that you can build libraries of X2Shell applets

and utilize them to make more complex applications.


