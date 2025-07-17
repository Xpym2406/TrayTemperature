# Fork of Fergo TrayTemperature

A very simple CPU and GPU temperature monitor for the system tray.

![TrayTemperature](<img width="247" height="286" alt="изображение" src="https://github.com/user-attachments/assets/9693896b-4629-47f9-ae92-7dd98f179177" />)

Fork of https://github.com/Fergo/TrayTemperature/tree/v0.2

i just wanted to upgrade it and make it work with ryzen 9xxx
# Usage

Just run  `TrayTemperature.exe` and a tray icon will be added displaying the CPU temperature (top) and GPU temperature (bottom). 

The application requires elevated priviledges in order to properly acquire the sensor data.

# Customization

To customize the temperature ranges and their different colors, edit the following section in the `TrayTemperature.exe.config` file:

```xml
<setting name="CPUMed" serializeAs="String">
	<value>#ffff00</value>
</setting>
<setting name="CPULow" serializeAs="String">
	<value>#ffffff</value>
</setting>
<setting name="CPUHigh" serializeAs="String">
	<value>#ff0000</value>
</setting>
<setting name="CPUTempMed" serializeAs="String">
	<value>65</value>
</setting>
<setting name="CPUTempHigh" serializeAs="String">
	<value>80</value>
</setting>
<setting name="GPULow" serializeAs="String">
	<value>#ffffff</value>
</setting>
<setting name="GPUMed" serializeAs="String">
	<value>#ffff00</value>
</setting>
<setting name="GPUHigh" serializeAs="String">
	<value>#ff0000</value>
</setting>
<setting name="GPUTempMed" serializeAs="String">
	<value>60</value>
</setting>
<setting name="GPUTempHigh" serializeAs="String">
	<value>85</value>
</setting>
```

# Download

Check the **Releases** page: 
https://github.com/Xpym2406/TrayTemperature/releases

# Source code requirements 

This software makes use of LibreHardwareMonitor.

https://github.com/openhardwaremonitor/openhardwaremonitor

The pre-compiled dll is already available with the release of TrayTemperature.

