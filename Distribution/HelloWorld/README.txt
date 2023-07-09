HELLOWORLD
=============

This is a simple start of an X2Shell application.  

Notice that HelloWorld.exe is simply a copy of X2Shell.exe.

However, since there is ALSO a HelloWorld.xml in the same folder, X2Shell uses this for its configuration.

We could have also made a .BAT file called HelloWorld.bat that simply had the following, which would do the same thing.


	@X2Shell.exe %$ /xml HelloWorld.xml
	

NOTE: The leading @ is simply to prevent the batch file from echoing the command to the screen.
NOTE: The %$ is to have any command-line parameters brought forward to the call to X2Shell.
