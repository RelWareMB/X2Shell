<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:msxsl="urn:schemas-microsoft-com:xslt">

<xsl:template match="/">
	<!-- 
	    We are using this single transform for several purposes, 
		simply to make distribution of this sample configuration easy.
		Any production system should have individual transforms for specific purposes
	-->
	<xsl:choose>
		<!-- 
			View the Datacloud?  
			(NOTE: Due to the single fashion of this transform, this ALWAYS must be the first choice) 
			(AGAIN: This should not be done in a production environment.)
		-->		
		<xsl:when test="/X2Shell/pause[@id='ViewDataCloud']">
			<DataCloud>
				<dump-log>
					<params filepath="{/X2Shell/run-time/environment/env-var[@name='TEMP']}\X2Shell_DataCloud.xml"/>
				</dump-log>
				<command>
	     				<params command="{/X2Shell/run-time/environment/env-var[@name='TEMP']}\X2Shell_DataCloud.xml"/>
				</command>
			</DataCloud>
		</xsl:when>

		<!-- Wanting to Create a new application -->		
		<xsl:when test="/X2Shell/pause[@id='CreateNewApp?']">
	     	<Run><!-- AppName -->
				<!-- Note: Because we are using XSLT, we could have built the entire CMD.exe using the arguments attribute only.
					However, it should be illustrated that the <command> node params allows for additional arguments to be built
					from any combination of literal string values or xpath expressions.  Additionally, the <arg> elements can 
					override the default seperator of space (" ") with an empty string or any other string of their choosing.
				-->
				<command id="CopyApp"><!-- copy X2Shell.exe to new App name -->
					<params command="CMD.exe" arguments="/C" use-shell="false" wait-for-exit="true" capture-stdout="true">
		     			<arg>COPY</arg>
		     			<arg type="xpath" separator-character=" &quot;">/X2Shell/run-time/@app-location</arg>
		     			<arg type="xpath" separator-character="&quot; &quot;">/X2Shell/run-time/command-line/@cwd</arg>
		     			<arg separator-character="\" type="literal"><xsl:value-of select="/X2Shell/pause[@id='AppName']/text()"/>.exe</arg>
		     			<arg separator-character="&quot; ">/B</arg>
		     		</params>
				</command>
				<save-file id="AppTemplate"><!-- copy App Template (Hello World) to new app name. -->
					<params filepath="{/X2Shell/pause[@id='AppName']/text()}.xml" rootnode-xpath="/X2Shell/Data[@id='AppTemplate']/*" format="xml"/>
				</save-file>
			</Run>
		</xsl:when>


		<!-- Wanting to View application usage -->		
		<xsl:when test="/X2Shell/pause[@id='ViewUsage']">
	     	<Run>
				<command id="GetUsage">
					<params command="{/X2Shell/run-time/@app-location}" arguments="/?" use-shell="false" wait-for-exit="true" capture-stdout="true" />
				</command>
				<xsl:choose>
					<xsl:when test="/X2Shell/pause[@id='ViewUsingNotepad']/@dialog-result != 'yes'">
				     	<echo>
							<string type="xpath">/X2Shell/command[@id='GetUsage']/stdout/text()</string>
						</echo>
					</xsl:when>
					<xsl:otherwise>
						<save-file id="PersistUsage">
							<params filepath="{/X2Shell/run-time/environment/env-var[@name='TEMP']}\X2Shell_README.txt" rootnode-xpath="/X2Shell/command[@id='GetUsage']/stdout" file-type="text"/>
						</save-file>
						<command id="NotepadUsage">
							<params command="notepad.exe" arguments="{/X2Shell/run-time/environment/env-var[@name='TEMP']}\X2Shell_README.txt" use-shell="true" wait-for-exit="false" />
						</command>
					</xsl:otherwise>
				</xsl:choose>
			</Run>
		</xsl:when>



		<!-- Performing the IPCONFIG test -->
		<xsl:when test="/X2Shell/command[@id='IPCONFIG']">
			<IPCONFIG>
			<xsl:variable name="IPCONFIG" select="/X2Shell/command[@id='IPCONFIG']/stdout/text()"/>
			<xsl:variable name="Adapters"><xsl:call-template name="ExtractAdapters"><xsl:with-param name="Source" select="$IPCONFIG"/></xsl:call-template></xsl:variable>
			<xsl:copy-of select="msxsl:node-set($Adapters)"/>
			</IPCONFIG>
		</xsl:when>


		<!-- Performing the MSMQ test (Step 1) -->
		<xsl:when test="/X2Shell[( count(pause[@id='QueueName']) = 1 ) and ( count(msmq) = 0 )]">
			<msmq id="LIST">
				<params queue-name="{/X2Shell/pause[@id='QueueName']/text()}" method="list" format="text"/>
			</msmq>
		</xsl:when>
		<!-- MSMQ (Step 2) -->
		<xsl:when test="/X2Shell[( count(pause[@id='QueueName']) = 1 ) and ( count(msmq) = 1 )]">
			<Run>
				<xsl:choose>
					<xsl:when test="count(/X2Shell/msmq/Error) = 0">
						<echo>
							<line><xsl:value-of select="/X2Shell/msmq/@queue-name"/><![CDATA[		]]><xsl:choose><xsl:when test="count(/X2Shell/msmq/message) = 0">EMPTY</xsl:when><xsl:otherwise><xsl:value-of select="count(/X2Shell/msmq/message)"/> message(s)</xsl:otherwise></xsl:choose></line>
							<xsl:for-each select="/X2Shell/msmq/message">
								<line><xsl:value-of select="position()"/>: <xsl:value-of select="@msg-id"/><![CDATA[	]]>(<xsl:value-of select="@msg-type"/>)<![CDATA[	]]><xsl:choose><xsl:when test="/X2Shell/msmq/@format='text'"><xsl:value-of select="string-length(.)"/> bytes</xsl:when><xsl:otherwise>XML: <xsl:value-of select="name(*)"/></xsl:otherwise></xsl:choose></line>
							</xsl:for-each>
						</echo>
						<xsl:choose>
							<xsl:when test="count(/X2Shell/msmq/message) &gt; 0">
								<pause id="SaveToDisk?">
									<params method="msgbox" text="Would you like to save these messages as files?" caption="Save Messages as Files?" buttons="yes-no" icon="question"/>
								</pause>
								<skip-to test="/X2Shell/pause[@id='SaveToDisk?']/@dialog-result != 'yes'">
									<params method="id" value="ClearQueue?"/>
								</skip-to>
								<xsl:for-each select="/X2Shell/msmq/message">
									<save-file id="msg_{@msg-id}">
										<params filepath="{translate(@msg-id, '\', '_')}.message" rootnode-xpath="/X2Shell/msmq/message[@msg-id = '{@msg-id}']" format="text"/>
									</save-file>
								</xsl:for-each>
								<pause id="ClearQueue?">
									<params method="msgbox" text="Would you like to clear these messages from the queue?" caption="Clear Messages from Queue?" buttons="yes-no" icon="question"/>
								</pause>
								<skip-to test="/X2Shell/pause[@id='ClearQueue?']/@dialog-result != 'yes'">
									<params method="end"/>
								</skip-to>
								<xsl:for-each select="/X2Shell/msmq/message">
									<msmq id="ClearQueue.{position()}">
										<params queue-name="{/X2Shell/msmq/@queue-name}" method="receive" format="text"/>
									</msmq>
								</xsl:for-each>
							</xsl:when>
							<xsl:otherwise>
								<pause id="CreateMsg?">
									<params method="msgbox" text="Since there were no messages, would you like to create a sample message using this DataCloud?" caption="Create Message?" buttons="yes-no" icon="question"/>
								</pause>
								<msmq test="/X2Shell/pause[@id='CreateMsg?']/@dialog-result = 'yes'">
									<params method="send" queue-name="{/X2Shell/msmq/@queue-name}" format="xml" rootnode-xpath="/X2Shell"/>
								</msmq>
								<echo test="/X2Shell/pause[@id='CreateMsg?']/@dialog-result = 'yes'">
									<line>Message Sent.</line>
									<line>When asked to Restart, select yes, and enter the queue name again.</line>
								</echo>									
							</xsl:otherwise>							
						</xsl:choose>
					</xsl:when>
					<xsl:otherwise>
						<echo>ERROR: <xsl:value-of select="/X2Shell/msmq/Error/text()"/></echo>
					</xsl:otherwise>
				</xsl:choose>
			</Run>
		</xsl:when>

		<!-- Performing the LIST test -->
		<xsl:when test="/X2Shell/pause[@id='FilePath']">
			<LIST>
				<path>
					<params command="list" filepath="{/X2Shell/pause[@id='FilePath']/text()}"/>
				</path>
			</LIST>
		</xsl:when>



		<!-- Performing the MAIL test -->
		<xsl:when test="/X2Shell/pause[@id='FromAddr']">
			<MAIL>
				<params smtp-host="{/X2Shell/pause[@id='Server']/text()}" from="{/X2Shell/pause[@id='FromAddr']/text()}" from-name="X2Shell Sample Configuration Test" to="{/X2Shell/pause[@id='ToAddr']/text()}" subject="X2Shell Sample Configuration Test Mail" rootnode-xpath="/X2Shell/transform[@id='MAIL']/MAIL/message" format="text">
				<xsl:attribute name="credentials"><xsl:choose><xsl:when test="/X2Shell/pause[@id='UseAuthentication?']/@dialog-result = 'yes'">network</xsl:when><xsl:otherwise>none</xsl:otherwise></xsl:choose></xsl:attribute>
				</params>
				<message>
					<xsl:for-each select="/X2Shell/echo[@id='CommonHeader']"><xsl:value-of select="@text"/><![CDATA[
]]></xsl:for-each><![CDATA[
This sample message is coming to you from X2Shell.
Please visit our website (www.x2a2.com?x2shell) for more information.

If you did not intend to receive this message, please contact the sender of this e-mail.

]]><xsl:value-of select="/X2Shell/@powered-by"/><![CDATA[
]]><xsl:value-of select="/X2Shell/@legal-notice"/>
				</message>
			</MAIL>
		</xsl:when>



		<!-- Performing the UPDATE test -->
		<xsl:when test="/X2Shell/http[@id='CURRENT_VERSION']">
			<Run id="Check4Updates">
			<xsl:variable name="Local_Info" select="/X2Shell/load-file[@id='LOCAL']/XML_DIZ_INFO/Program_Info"/>
			<xsl:variable name="Current_Info" select="/X2Shell/http[@id='CURRENT_VERSION']/XML_DIZ_INFO/Program_Info"/>
			<xsl:variable name="Local_Version" select="$Local_Info/Program_Version"/>
			<xsl:variable name="Local_ReleaseDate"><xsl:value-of select="$Local_Info/Program_Release_Year"/><xsl:value-of select="$Local_Info/Program_Release_Month"/><xsl:value-of select="$Local_Info/Program_Release_Day"/></xsl:variable>
			<xsl:variable name="Current_Version" select="$Current_Info/Program_Version"/>
			<xsl:variable name="Current_ReleaseDate"><xsl:value-of select="$Current_Info/Program_Release_Year"/><xsl:value-of select="$Current_Info/Program_Release_Month"/><xsl:value-of select="$Current_Info/Program_Release_Day"/></xsl:variable>
			<xsl:choose>
				<xsl:when test="number($Current_ReleaseDate) &gt; number($Local_ReleaseDate)">
					<xsl:variable name="Local_DisplayDate"><xsl:value-of select="$Local_Info/Program_Release_Month"/>/<xsl:value-of select="$Local_Info/Program_Release_Day"/>/<xsl:value-of select="$Local_Info/Program_Release_Year"/></xsl:variable>
					<xsl:variable name="Current_DisplayDate"><xsl:value-of select="$Current_Info/Program_Release_Month"/>/<xsl:value-of select="$Current_Info/Program_Release_Day"/>/<xsl:value-of select="$Current_Info/Program_Release_Year"/></xsl:variable>
					<pause id="Go2Download?">
     						<params method="msgbox" text="This application is no longer current.
This is running v{$Local_Version} released on {$Local_DisplayDate}.
The current release is v{$Current_Version} released on {$Current_DisplayDate}.

Would you like to view the download page to get the latest version?" caption="Go to download page?" buttons="yes-no" icon="question"/>
					</pause>
					<command test="/X2Shell/pause[@id='Go2Download?']/@dialog-result = 'yes'">
						<params command="http://www.x2a2.com/?downloads"/>
					</command>
				</xsl:when>
				<xsl:otherwise>
					<pause>
     						<params method="msgbox" text="This application (v{$Current_Version}) is up-to-date.
Thank you for checking." caption="Current version" buttons="ok-only" icon="information"/>
					</pause>
				</xsl:otherwise>
			</xsl:choose>
			</Run>
		</xsl:when>

		<!-- Performing the INFO test -->
		<xsl:when test="/X2Shell/load-file[@id='INFO']">
			<xsl:apply-templates select="/X2Shell/load-file/XML_DIZ_INFO"/>
		</xsl:when>

</xsl:choose>

</xsl:template>


<xsl:template name="ExtractAdapters">
<xsl:param name="Source"/>
<xsl:choose>
	<xsl:when test="string-length(substring-after($Source, ' adapter ')) &gt; 0">
		<xsl:variable name="adapterName" select="substring-before(  substring-after($Source, ' adapter ') , ':')"/>
		<xsl:variable name="remainder" select="substring-after(  substring-after($Source, ' adapter ') , ':')"/>
		<xsl:variable name="dontCare" select="substring-before($remainder, 'Address')"/>
		<xsl:variable name="addressBlock" select="substring-after($remainder, 'Address')"/>
		<xsl:variable name="address" select="substring-after(substring-before($addressBlock, '&#x0A;'), ':')"/>
		<xsl:if test="string-length($address) &gt; 0">
			<adapter name="{$adapterName}" address="{$address}"/>
		</xsl:if>
		<xsl:call-template name="ExtractAdapters">
			<xsl:with-param name="Source" select="substring-after($addressBlock, '&#x0A;')"/>
		</xsl:call-template>
	</xsl:when>
</xsl:choose>
</xsl:template>


<xsl:template match="XML_DIZ_INFO">
<PAD>
<Text>
========================================================<xsl:apply-templates select="Program_Info"/>
--------------------------------------------------------<xsl:apply-templates select="Company_Info"/>
--------------------------------------------------------<xsl:apply-templates select="Program_Descriptions"/>
--------------------------------------------------------<xsl:apply-templates select="Web_Info"/>
--------------------------------------------------------<xsl:apply-templates select="MASTER_PAD_VERSION_INFO"/>
========================================================
</Text>
<EULA><xsl:value-of select="Permissions/EULA"/></EULA>
</PAD>
</xsl:template>


<xsl:template match="Program_Info">
Program Name: <xsl:value-of select="Program_Name"/>		Version: <xsl:value-of select="Program_Version"/>
Released on:  <xsl:value-of select="Program_Release_Month"/>/<xsl:value-of select="Program_Release_Day"/>/<xsl:value-of select="Program_Release_Year"/>	Release Type: <xsl:value-of select="Program_Release_Status"/>
Licence Type: <xsl:value-of select="Program_Type"/>		Language: <xsl:value-of select="Program_Language"/>
OS Support:   <xsl:value-of select="Program_OS_Support"/>
System Reqs:  <xsl:value-of select="Program_System_Requirements"/>
Category:     <xsl:value-of select="Program_Specific_Category"/>::<xsl:value-of select="Program_Category_Class"/>&#x0D;&#x0A;
</xsl:template>

<xsl:template match="Company_Info">
Company Name: <xsl:value-of select="Company_Name"/>
     Address: <xsl:value-of select="Address_1"/><xsl:if test="string-length(Address_2) &gt; 0">, <xsl:value-of select="Address_2"/></xsl:if>
            : <xsl:value-of select="City_Town"/>, <xsl:value-of select="State_Province"/><![CDATA[  ]]><xsl:value-of select="Zip_Postal_Code"/>
            : <xsl:value-of select="Country"/>
     Website: <xsl:value-of select="Company_WebSite_URL"/>
</xsl:template>

<xsl:template match="Web_Info">
Get more information on the Internet at the following locations:
Information:  <xsl:value-of select="Application_URLs/Application_Info_URL"/>
Order screen: <xsl:value-of select="Application_URLs/Application_Order_URL"/>
Download:     <xsl:value-of select="Download_URLs/Primary_Download_URL"/>
App. Icon:    <xsl:value-of select="Application_URLs/Application_Icon_URL"/>
PAD file:     <xsl:value-of select="Application_URLs/Application_XML_File_URL"/>
</xsl:template>

<xsl:template match="Program_Descriptions"><![CDATA[
]]><xsl:value-of select="English/Char_Desc_80"/>
Keywords:     <xsl:value-of select="English/Keywords"/>
</xsl:template>


<xsl:template match="MASTER_PAD_VERSION_INFO">
PAD Editor:   <xsl:value-of select="MASTER_PAD_EDITOR"/><![CDATA[
]]><xsl:value-of select="MASTER_PAD_INFO"/>
</xsl:template>

</xsl:stylesheet>