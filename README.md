# X2Shell  
## *A Simple Way to Build Complex Tools*

**X2Shell** brings the power of **RelWare's *X2* technologies** to your Shell / Console processing needs. It utilizes an XML configuration with a simple language-set, allowing it to automate anything from simple DOS tasks to complex operations and support jobs.

Code named * **B**atch **R**unner **&** **D**iagnostics for **O**perating / **N**etworking * *(BRANDON)*, and re-branded as **X2Shell**,
this is an application for building other applications and toolsets.  **X2Shell** uses one or more XML file(s) for both its configuration and instructions.
By default, it looks for an XML file by the same name and path as its.EXE to use as the configuration/instructions.
*(See [command line switches](#command-line-switches) at end of this document for other options.)*

The configuration (XML) file should consist of any valid root element, which then
contains one or more of any combination of **Data** or **Run** child elements, for inline-data, and execution instructions, respectively.  It may also contain one or more **Finally** child elements, which will be treated exactly as a **Run** element, except that it will be executed *AFTER* standard processing is complete. Each of these elements, as well as any contained instruction, may contain a
**@test** attribute, to determine if it should be added / performed.  The test should be in the style of an xpath to a valid node in the current [DataCloud](#The-DataCloud).  A test with a "hit" *(aka "exists")* is a pass and a "miss" *(aka "no match")* is a fail.  If no **@test** attribute exists, then it is a pass.

Each **Data** node that passes will be copied as is into the [DataCloud](#datacloud).
Each **Run** node that passes will have its child executions performed, presuming their tests pass.  A **Run** element, as well as the **run-batch** execution below, can contain one or more of any any combination of the below [execution types](#List-of-Executions).

To allow better handling of complex binary data blobs, there is an additional component called the **[ByteArray](#byte-array)** which different executions and parameters can utilize as their input or result.
______________________________________________________________________________
## The DataCloud

The **DataCloud** is a concept used throughout most ***X2* technologies**.  Think of the **DataCloud** as a growing knowledge base for your program, each time it starts.  The **DataCloud** is itself a living XML document that gets created on each run of the application, and starts with simple information about the environment, including the system-time, run-time parameters that were given, environment variables such as PATH and the like.  Then, as your program executes, any **Data** blocks are added, such as hard-coded configuration variables.  Additionally, *EVERY* execution *ALSO* gets added to the **DataCloud** with its results.  In this way, the **DataCloud** acts as both a living audit-log of the executions, as well as a point-of-truth for data on any further executions.  For instance, you can use the [sql](#sql) execution to perform a "login" and get back data a username to use in further executions.  Then, rather than attempt to keep global variables and pass them as parameters, you can simply code all subsequent executions to use the same xpath to username obtained from the sql data.

Following is a trimmed down version of a standard **DataCloud** upon execution startup:

   

    <X2Shell>
    	<run-time 
  	    	app-location="C:\RelWare\Tools\HelloWorld\HelloWorld.exe" 
      		app-path="C:\RelWare\X2Shell\Distribution\HelloWorld" 
      		systemtime="7/9/2023 1:52:09 PM" 
            ...
 	    >
            <command-line 
        			cwd="C:\RelWare\Tools\HelloWorld" 
        			text="HelloWorld.exe">
                <arguments>
                  <arg index="0">HelloWorld.exe</arg>
                </arguments>
            </command-line>
            <system
    			machine-name="WIN-DEVELEOPER" 
    			os-version="Microsoft Windows NT 6.1.7601 Service Pack 1" 
    			processor-count="4" 
    			username="Developer" 
    			domain="RELWARE" 
                ...
            />
            <environment>
              <env-var name="COMPUTERNAME">WIN-DEVELEOPER</env-var>
              <env-var name="USERPROFILE">C:\Users\Developer</env-var>
              <env-var name="HOMEPATH">\Users\Developer</env-var>
              <env-var name="PROCESSOR_ARCHITECTURE">AMD64</env-var>
              <env-var name="PROCESSOR_LEVEL">6</env-var>
	    	  ...
            <environment>
        </run-time>




**NOTE:** Unless otherwise configured, the resultant data cloud will have a root
element named **X2Shell**.  To change this, include an attribute named
**@root-node** with a desired value in the root node of your configuration XML,
or include the attribute **@use-root** with a value of either **yes** or **true** to
use the name of your configuration XML root node as the DataCloud root name.
______________________________________________________________________________

## Byte Array

The **ByteArray** is a single BLOB *(Binary Large Object)* that can be utilized to pass binary data between executions.  It is created fresh on every program startup, and disposed at the end of the application.

See related commands:

- [clear-bytes](#clear-bytes)
- [compress-bytes](#compress-bytes)
- [decompress-bytes](#decompress-bytes)
- [sql](#sql)


______________________________________________________________________________


List of Executions
==============================================================================
## *(aka "Command Set")*
- [command](#command)
- [clear-bytes](#clear-bytes)
- [compress-bytes](#compress-bytes)
- [decompress-bytes](#decompress-bytes)
- [compress](#compress)
- [dump-log](#dump-log)
- [echo](#echo)
- [form](#form)
- [http](#http)
- [load-config](#load-config)
- [load-file](#load-file)
- [mail-to](#mail-to)
- [msmq](#msmq)
- [new-id](#new-id) *(aka [guid](#new-id) )*
- [output](#output)
- [path](#path)
- [pause](#pause)
- [powershell](#powershell)
- [registry](#registry)
- [reset](#reset)
- [run-batch](#run-batch)
- [save-file](#save-file)
- [skip-to](#skip-to)
- [stop-if](#stop-if)
- [sql](#sql)
- [transform](#transform)
- [zip](#zip) *(aka [zip-files](#zip) )*
- [unzip](#unzip) *(aka [unzip-files](#unzip) )*
- [unzip-files](#unzip-files)
- [list-zip](#list-zip) *(aka [read-zip](#list-zip) )*
- [x2tl](#x2tl)
______________________________________________________________________________
## command
*Launch a new process / executable or shell extension*

**Params:**

        command="some.exe"
        working-directory="c:\"
        arguments="/arg1 /arg2"           (NOTE: see below)
        use-shell="true|false"
        style="min|max|hidden|normal"
        wait-for-exit="true|false"
        capture-stdin="true|false"        (NOTE: see below)
        capture-stdout="true|false"
        capture-stderr="true|false"

**NOTE:** Additional arguments may be given as **arg** child elements within the **params**.
These **arg** children should have their text reflecting the value to be used.
Further, **arg** nodes may have **@type** and **@format** attributes, which will act
exactly as the **\_type** and **\_format** suffixed attributes explained below.

For more flexibility, the **arg** elements also support two additional attributes:
**@no-preceding-whitespace** and **separator-character**.  Giving the first of these
a **true** value will simply instruct the argument builder to ***NOT*** add a space
between it and the previous **arg** node.  Additionally, you can override the space separator to your own character *(i.e. separator-character="+")*.


**NOTE:** Capturing *(aka Re-directing)* Standard Input is *ONLY* available when also including the option **@use-shell="true"**.  Further, an additional child element **stdin** must be provided within the **params** parent node.  Like the **arg** nodes above, it supports **@type** and **@format**.

______________________________________________________________________________
## clear-bytes
*Used to empty out the binary data byte array (bytes)*

**Params:**

    (None)

Clears out the "bytes" byte array when the contents are no longer needed.

______________________________________________________________________________
## compress-bytes
*Used to compress the binary data byte array (bytes)*

**Params:**

    (None)

Performs ZIP compression on the "bytes" byte array, compressing the contents

______________________________________________________________________________
## decompress-bytes
*Used to empty out the binary data byte array (bytes)*

**Params:**

    (None)

Performs ZIP decompression on the "bytes" byte array, exploding the contents

_____________________________________________________________________________
## compress
*Used to ZIP a file*

**Params:**

			filepath="compressed.zip"
			mode="create|update"
			source-filepath="some-file-to-compress"
			saveas-filepath="directory/path/file_name.ext"
			delete-onsuccess="false|true"

Either create a new or update an existing ZIP file by adding the source file
and saving it in the ZIP as the specified filepath.  If **@delete-onsuccess**,
then the ***source*** file will be deleted after compression.

______________________________________________________________________________
## dump-log
*Used to dump the current datacloud out to a file or console*

**Params:**

			filepath="output.xml" (or empty for console)
			rootnode-xpath="xpath-to-element" (default = "/")

Write out all or part of the XML of the datacloud

_____________________________________________________________________________
## echo
*Write some text out to the console, or file (see [output](#output))*

Unlike other executions, the **echo** type does ***NOT*** employ a **params** child.

Instead, there are two methods of calling **echo**, the first being with a simple block of text.  In this case, there is no **params** element, but only the text to be output, with optional new-line indicator.

		<echo  new-line="true|false">
            any text to be output 
        </echo>

The second method involves building a complex **echo** statement by including individual **string** and/or **line** elements, each having their own **@test** and/or **@xpath** attributes as well as **@no-preceding-whitespace**.

        <echo>
           <string>Welcome</string>
           <string type="xpath">/X2Shell/sql/result/@username</string>
           <string no-preceding-whitespace="true">!</string>
           <line/>
           <line>Let's get to it!</line>
        </echo> 

**NOTE:** As **X2Shell** supports complex XPath statements, the above could *ALSO* have been written as follows:

        <echo>
           <line type="xpath">concat('Welcome ', /X2Shell/sql/result/@username, '!')</line>
           <line>Let's get to it!</line>
        </echo>


_____________________________________________________________________________
## form
*Build a GUI Dialog Box and capture the user input for further processing*

Unlike most others, the **form** execution type does *NOT* have a **params** node,
but instead allows for a complete UI form to be defined in the XML as a *required* child
element named **gui**, with the following optional attributes
			

		<gui
	        background-color=""
			control-box="true|false"
			minimize-box="true|false"
			maximize-box="true|false"
			auto-scroll="true|false"
			show-icon="true|false"
			show-in-taskbar="true|false"
			icon="path-to-ico-file"
			background-image="path-to-jpeg-file"
			width="300"
			height="200"
        >
			
A **gui** element can have one or more **layout** elements, which can have the following attributes:

        <layout
    		left="0"
	    	top="0"
	    >
	    		
Each **layout** can then have any combination of the following elements with their respective attributes:
			
- **label**

		use-mnemonic="false"
		border-style="none"

- **numeric**

		(N/A)
Allow for numeric input
			
- **date**

		min-date=""
		max-date=""
		show-up-down="false|true"
Allow for date field input
			
- **textbox**

		border-style="none"
		scroll-bars="both"
		multi-line="true"
		password="false"
		password-char=""
		read-only="false"
Allow for simple or large text input

- **button**

		use-mnemonic="false"
		style="flat"
		use-style-backcolor="true"
		type="none|yes|no|abort|retry|cancel|ignore"
		form-accept="false"
		form-cancel="false"
Provide action buttons and capture their response
			
- **image**

		path=""
		border-style="none"
Add simple images to your layout

- **checkbox**

		auto-check="true|false"
		appearance"normal"
		auto-ellipsis="true|false"
		checked="false|true"
		check-state="checked|unchecked"
Allow for checkbox input (aka "check all that apply...")

- **radio**

		auto-check="true|false"
		appearance"normal"
		auto-ellipsis="true|false"
		checked="false|true"
Allow for radio select input (aka "pick ONLY one...")

- **combobox**  

		drop-down-width=""
**NOTE:** Should contain one or more **item** elements *(see below)*
Provide a drop-down list for the user to select from 

			
- **listbox**

		allow-multiple="false|true"
**NOTE:** A **listbox** should contain one or more **item** elements *(see below)*
Provide a fully expanded list for the user to select from 

- **checked-listbox**

		allow-multiple="false|true"
**NOTE:** A **checked-listbox** should contain one or more **item** elements *(see below)*
Provide a fully expanded list for the user to select from, and show checkboxes for the selected items 

- **item** *(child of one of the list boxes above)*

		caption=""

			

_____________________________________________________________________________
## http
*Used to get or post contents via HTTP web request*

**Params:**

		method="get|post"
		uri="http://...."
		post-rootnode-xpath="/" (if method is "post", what is sent)
		post-format="text|xml"  (and what format is it sent in)

_____________________________________________________________________________
## load-config
*Used to load the contents of the active configuration*

**Params:**

*(None)*

_____________________________________________________________________________
## load-file

*Used to load the contents of a file into the datacloud*

**Params:**

			filepath="somefilename"
			file-type="text|xml|base64"

_____________________________________________________________________________
## mail-to
*Used to send results to one or more e-mail recipients.*

**Params:**

			smtp-host=""   (either IP address or DNS name of SMTP server)
			tcpip-port="25" (optional - can change for custom port)
			from=""         (email address of sender)
			to=""           (one or more email addresses of recipients)
			subject=""
			credentials="network|none" (if server requires authorization)
			uri="http://...."
			rootnode-xpath="/"      (path to what is being sent)
			format="text|xml"       (and what format is it sent in)

_____________________________________________________________________________
## msmq
*Used to send, receive or list contents of a Microsoft Message Queue.*

**Params:**

			queue-name=".\Private$\MyQueue"   (path/name of MSMQ)
			method="send|receive|list"  (note: "list" is non-destructive)
			rootnode-xpath="/"     (if "send", path to what is being sent)
			format="text|xml"      (and what format is it sent in)
			priority="lowest|very-low|low|normal|above-normal|high|very-high|highest"  (default is "normal")

_____________________________________________________________________________
## new-id
*(aka guid) Used to generate one or more Globally Unique IDs*

**Params:**

			count="1"

_____________________________________________________________________________
## output
*Specify where **echo** command content should be sent*

**Params:**

			method="console|file|both|close"
			filepath="somefilepath"
			filemethod="create|append"

The **output** command with the **method** of **file** *(or **both**)*, forces the output of all echo statements from that point forward to be written to the **filepath** specified. 
If the **filemethod** is set to **append**, then the output will be appended to any existing contents in the file.  Otherwise, an existing file will be overwritten.

The redirecting **echo** output will continue until either a new **output**
command is given, or the program terminates.

_____________________________________________________________________________
## path
*Used to get or set the working dir, or list the contents of a path*

**Params:**

			command="get-cwd|set-cwd|list|exists|file-exists|...
							... copy|move[-file]|move-dir[ectory]|delete|make-directory|make-dir|mkdir"
			filepath="somefilepath" (optional - "cwd" is default)
			destpath="somefilepath" (only used for "copy" or "move")

This is used to manipulate a local drive or network drive path, or context.


_____________________________________________________________________________
## pause
*Hold processing for some intervention*

**Params:**

			method="console|msgbox|timer"
			timeout="number-in-milliseconds" (for timer only)
			caption=""
			text=""
			buttons="ok|ok-cancel|yes-no|yes-no-cancel|retry-cancel|abort-retry-cancel"

The operation of the **pause** execution depends upon the **method** given.
If the **method** is **console** (default) then this operates exactly as the "Press any key..." pause command (without the prompt, that is up to you).
If the **method** is **timer**, then the operation will resume after the **timeout** has occurred.
The **msgbox** option of **method** is the most UI friendly, as an actual Dialog Box is presented to the user with **caption**, **text** and **button** options are provided.  Further operations can then be flexed based on the button value selected. *(ie. test="/X2Shell/pause/@dialog-result != 'cancel']" )

_____________________________________________________________________________
## powershell
*Run a PowerShell script or applet*

**Params:**

			working-directory="c:\"
			capture-stdout="true|false"

**NOTE:** See definition for the "command" action for argument usage

______________________________________________________________________________
## registry
*Manage the machine registry (get, set, create, list)*

**NOTICE: USE WITH CAUTION!  
*WITH GREAT POWER COMES GREAT RESPONSIBILITY!***

**Params:**

			hkey="HKLM|HKCU|HKCR|HKU|HKCC|HKEY_LOCAL_MACHINE|HKEY_CURRENT_USER|
							HKEY_CLASSES_ROOT|HKEY_USERS|HKEY_CURRENT_CONFIG"
			subkey=""
			method="get(-value)|set(-value)|create(-key)|list"
			value-name=""
			value-type="REG_SZ|REG_DWORD"
			value-data=""
			format="text|xml"

Either **get**, **set**, **create** or **list** *(aka read)* a **subkey** within a branch of the registry.

_____________________________________________________________________________
## reset
*Resets the DataCloud to remove all prior execution information*

**Params:**

			method="soft|hard"

Both resets will clear the DataCloud, but use a different manner.  The soft
reset will remove all prior exeuction nodes.  The hard reset will rebuild the
DataCloud from scratch.  
**NOTE:** A hard reset will also update the **DataCloud** **systemtime** element.

_____________________________________________________________________________
## run-batch
*Process another batch of commands (often those created from a **[transform](#transform)**)*

**NOTE:** Unlike most other execution types, the **run-batch** does *NOT* use a **params** child.
Instead, the child elements are themselves executions.  Consider a **run-batch** to act *EXACTLY* like a parent **Run** block.

	{ List of commands to be run }

**NOTE:** Most uses of the run-batch choose to instead employ the xpath attribute to execute a block of dynamically generated commands. *(See **[transform](#transform)** or **[x2tl](#x2tl)**)*


    <run-batch xpath="/X2Shell/transform/my-command-block"/>

_____________________________________________________________________________
## save-file
*Save the contents of part or all of the datacloud to file*

**Params:**

			filepath=""             (path/name of file to be created)
			rootnode-xpath="/"            (path to what is being saved)
			format="text|xml"             (and what format it is saved in)
			append-contents="true|FALSE"  (default = override contents)

_____________________________________________________________________________
## skip-to
*Skip processing if this command is run*

**Params:**

			nextCommandType="{ any valid command type }" (i.e. "pause")
			nextCommandId="{ id of an upcoming command }"
			end="true" (actual value is ignored, must be non-empty text)

This allows the execution of the application to skip current processing *("Do no pass Go" style)* and resume either at an upcoming execution type or named ID, or to the end of processing (ie. stop standard processing and go to [Finally](#Finally).

_____________________________________________________________________________
## stop-if
*Stop processing if this command is run*

**Params:**

			method="this-run|all"

Stop execution of either the current **Run** *(or **run-batch**)* or all standard executions, moving directly to any [Finally](#Finally) block(s) if they exist.
_____________________________________________________________________________
## sql
*Perform a Microsoft SQL Server Execution*

**Params:**

			method="recordset|xml|bytes|execute-only"
			server="sqlserver\instance"
			connect="DataSource=Connection_String"
			query="exec my_query @param1, @param2"
			row-label="mydataitem" (default: row)

For standard recordsets, the **row-label** *(default is **row**)* will be used to generate a new element for each entry in the resulting recordset, with the field names provided used as attributes.

**NOTE:** When using the **recordset** *(default)* method, do *NOT* use spaces or special
characters for the names of the columns, or you will generate an error.

**NOTE:** Use of the **bytes** return type will store the resulting single image 
response in the general byte array *(see [Byte Array](#byte-array))*.

**NOTE:** If the SQL result will be in the form of XML *(for instance using the FOR XML option of SQL)*
then set the **method** to **xml**, and the **row-label** attribute to indicate the name of the wrapper element that will be added to the DataCloud.

**NOTE:** If your query or stored procedure has parameters, include them as 
**param** child elements of the **params** element.  Give them a **name** 
attribute, a **value** attribute, and optionally a **test** attribute.
If no **value** attribute is given *(or an xpath that is a "miss")* will indicate a **NULL**. 
Give your **param** a **value_type** of **bytes** to use the [Byte Array](#byte-array).

Example:

  <sql id="..." test="" xpath="">
    <params server="" connect="" query="exec my_sproc @p1, @p2, @pImage">
      <param name="@p1" value="1"/>
      <param name="@p2" />
      <param name="@pImage" value_type="bytes"/>
    </params>
  </sql>

_____________________________________________________________________________
## transform
*Used to apply an XSLT transformation to all or a portion of the DataCloud*

There are three methods of using the **transform** execution type.

- The first is to transform all of part of the DataCloud against an external XSLT file.

**Params:**

		filepath="somefile.xsl"
		rootnode-xpath="/datacloud/xpath"

- The second method is transform an *external* Document against an external XSLT file.

**Params:**

		filepath="somefile.xsl"
		source-filepath="somedocument.xml"

- The third method is to transform all of part of the DataCloud against an ***inline*** XSLT.

**Params:**

		rootnode-xpath="/datacloud/xpath"
        <inline><xsl:stylesheet> .... </xsl:stylesheet></inline>
 


The XSLT will be applied to either the Datacloud, optionally at the rootnode-xpath, or external document, if specified.  In all cases, the result of the transform is then added to the DataCloud.

**NOTE:** If using **inline** ensure that your XSLT file is completely embedded, as show below.

		<transform>
			<params rootnode-xpath="/datacloud/xpath"
            <inline><xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
                <xsl:output method="xml" encoding="UTF-8"/>
				<xsl:template match="/">
                  <MyTransformResult> ... </MyTransformResult>
                </xsl:template>
			</xsl:stylesheet></inline>
		</transform>

_____________________________________________________________________________
## zip
*(aka zip-files) Creates or Updates a ZIP file from one or more files*

**Params:**

			filepath="some-files-to-zip*.*"
			destpath="my-zip-file.zip"
			action="ADD | move"
			overwrite="true | FALSE"

If the action is set to **move**, the source file will be **deleted** once zipped.
If **overwrite** is set to **true** then any pre-existing zip will be overwritten



_____________________________________________________________________________
## unzip
*(aka unzip-files) Expands a ZIP file to a specified directory*

**Params:**

			filepath="source-file-name.zip"
			destpath="file-path-to-extract-into"



_____________________________________________________________________________
## list-zip
*(aka read-zip) Lists the contents of a ZIP file*

**Params:**

			filepath="source-file-name.zip"



______________________________________________________________________________
## x2tl
*Apply an {{ [X2 Templating Language](http://relware.com/docs/x2tl) }} Template against the datacloud*

**NOTE:** Unlike most other execution types, **x2tl** does *NOT* use the **params** element.  Instead, the first child element is the start of the **X2TL Template** *(see [X2 Templating Language](http://relware.com/docs/x2tl))*

**Params:**

			execute-results="true|FALSE"

Example:

    <x2tl execute-results="true|FALSE">
        <myTemplate>
            <echo>Hello {{ = /X2Shell/sql/result/@username }}!</echo>
        </myTemplate>
    </x2tl>


______________________________________________________________________________

## HELLOWORLD

The following is a simple HelloWorld configuration.  
To replicate this on your own, simply copy **X2Shell.exe** to **HelloWorld.exe** and then save the following as **HelloWorld.xml** in the same folder.  Alternately, you can also run this by typing **X2Shell /xml HelloWorld.xml**

    <X2Shell>
      <Run>
        <echo>Hello World!</echo>
      </Run>
	</X2Shell>



______________________________________________________________________________

## ADDITIONAL TECH NOTES

## for-each

Each **Run** node may provide a **@for-each** attribute that specifies an
xpath to a subset of nodes to be run against.  The commands in the **Run** node
will then loop over each node matching the for-each context.  

**NOTICE:** When using **for-each** looping, all xpath parameter types will refer
to the ***Current Context Node***, whereas traditionally, all xpaths use the **root** node as context.

## id

Each execution may have an **@id** attribute, which will be carried forward to the **Datacloud**.

## xpath

Each execution may have an **@xpath** can point to a different path to be used for the **[params](#params)** node of the execution.  This allows for complex application building, as the parameters to be used for one execution can be built from the results of a different execution.

**NOTE:** In the case of the **echo** command, the xpath points to the text to be output.

## params

Each execution type has its own parameters, which are stored as attributes of the **params** child element.  Each parameter to be passed is represented by an attribute of the same name, such as: 

    <params  filename="myfile.txt" />

Additionally, an attribute can pass an xpath instead of a literal value, by including a second attribute with the parameter name followed by **\_type** and setting its value to **xpath**, such as:


    <params filename="/X2Shell/run-time/command-line/arguments/arg[@index='1']"
            filename_type="xpath"   />


Further, if an **xpath** is specified, then you can also include an additional **\_format** attribute, in the case that you need to send an XML block instead of a textual value.  A **\_format** attribute may have the values: **text, xml, **or** xml-outer** to indicate the format of the value that should be represented.


## command-line-switches

This program recognizes the following command-line parameters, and their use will **supercede** any usage by the running application:

	/?	 - Show this help screen
	/help	 - (ditto)
	/debug	 - Show step-by-step debug statements during processing
	/xml	 - The next parameter will be the path to the config XML.
	/console - Use the console stdin for the config XML.

**NOTE:** In order to support command-line-switches in your X2Shell applications 
that are themselves referenced by the **/xml** switch, it is suggested that you
create a .BAT file that executes as follows:

	@X2Shell %* /xml MyX2ShellApp.xml
			
This will pass any command-line paramaters given to your .BAT file into your
application, and allows you to handle them in order *(ie. /arg[@index=1] )*			

