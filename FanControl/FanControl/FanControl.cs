using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Management;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using FanControl.Utils;

// Modified version of https://github.com/SmokelessCPUv2/Lagon-Fan-EC-Control/blob/main/FanControl/Utils/Utils.cs
// Credits to SmokelessCpu for providing us with this file as open source. This is a better alternative to LFC as it uses
// WinRing in a more secure way and writting to the EC through the use of I/O ports. RwDrv.sys won't be used anymore compared to LFC
// nor will the service run 24/7
// Credits also to Underv0lti #5317 / akillisandalye (Discord) and SmokelessCPU for the EC mapping of Fan System
// File will keep previous enums by Smokeless in his test run app used for authenticity 
// This .cs file will allow to be built as an .exe and paired with WinRing0x64.dll to offer Fan Control on Legions Gen 5 and Gen 6
// Setting up the Fan Curve will be done through a config .txt file

namespace FanControl.Utils
{
    internal static class WinRing
    {
        //DLL
        public static bool WinRingInitOk = false;
        [DllImport("WinRing0x64.dll")]
        public static extern DLL_Error_Code GetDllStatus();
        [DllImport("WinRing0x64.dll")]
        public static extern UInt64 GetDllVersion(ref byte major, ref byte minor, ref byte revision, ref byte release);
        [DllImport("WinRing0x64.dll")]
        public static extern UInt64 GetDriverVersion(ref byte major, ref byte minor, ref byte revision, ref byte release);
        [DllImport("WinRing0x64.dll")]
        public static extern DriverType GetDriverType();
        [DllImport("WinRing0x64.dll")]
        public static extern bool InitializeOls();
        [DllImport("WinRing0x64.dll")]
        public static extern void DeinitializeOls();

        [DllImport("WinRing0x64.dll")]
        public static extern byte ReadIoPortByte(UInt32 address);

        [DllImport("WinRing0x64.dll")]
        public static extern void WriteIoPortByte(UInt32 port, byte value);

    }


    internal static class EC
    {
        //=======================================EC Direct Access interface=================================
        //Port Config:
        //  BADRSEL(0x200A) bit1-0  Addr    Data
        //                  00      2Eh     2Fh
        //                  01      4Eh     4Fh
        //
        //              01      4Eh     4Fh
        //  ITE-EC Ram Read/Write Algorithm:
        //  Addr    w   0x2E
        //  Data    w   0x11
        //  Addr    w   0x2F
        //  Data    w   high byte
        //  Addr    w   0x2E
        //  Data    w   0x10
        //  Addr    w   0x2F
        //  Data    w   low byte
        //  Addr    w   0x2E
        //  Data    w   0x12
        //  Addr    w   0x2F
        //  Data    rw  value

        public static void DirectECWrite(byte EC_ADDR_PORT, byte EC_DATA_PORT, UInt16 Addr, byte data)
        {
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
            WinRing.WriteIoPortByte(EC_DATA_PORT, 0x11);
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
            WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)((Addr >> 8) & 0xFF));

            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
            WinRing.WriteIoPortByte(EC_DATA_PORT, 0x10);
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
            WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)(Addr & 0xFF));

            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
            WinRing.WriteIoPortByte(EC_DATA_PORT, 0x12);
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
            WinRing.WriteIoPortByte(EC_DATA_PORT, data);
        }

        public static void DirectECWriteArray(byte EC_ADDR_PORT, byte EC_DATA_PORT, UInt16 Addr_base, byte[] data)
        {
            for (var i = 0; i < data.Count(); i++)
            {
                var Addr = (ushort)(Addr_base + i);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
                WinRing.WriteIoPortByte(EC_DATA_PORT, 0x11);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
                WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)((Addr >> 8) & 0xFF));

                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
                WinRing.WriteIoPortByte(EC_DATA_PORT, 0x10);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
                WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)(Addr & 0xFF));

                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
                WinRing.WriteIoPortByte(EC_DATA_PORT, 0x12);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
                WinRing.WriteIoPortByte(EC_DATA_PORT, data[i]);
            }
        }

        public static byte DirectECRead(byte EC_ADDR_PORT, byte EC_DATA_PORT, UInt16 Addr)
        {
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
            WinRing.WriteIoPortByte(EC_DATA_PORT, 0x11);
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
            WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)((Addr >> 8) & 0xFF));

            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
            WinRing.WriteIoPortByte(EC_DATA_PORT, 0x10);
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
            WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)(Addr & 0xFF));

            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
            WinRing.WriteIoPortByte(EC_DATA_PORT, 0x12);
            WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
            return WinRing.ReadIoPortByte(EC_DATA_PORT);
        }

        public static byte[] DirectECReadArray(byte EC_ADDR_PORT, byte EC_DATA_PORT, UInt16 Addr_base, int size)
        {
            var buffer = new byte[size];
            for (var i = 0; i < size; i++)
            {
                var Addr = (ushort)(Addr_base + i);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
                WinRing.WriteIoPortByte(EC_DATA_PORT, 0x11);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
                WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)((Addr >> 8) & 0xFF));

                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
                WinRing.WriteIoPortByte(EC_DATA_PORT, 0x10);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
                WinRing.WriteIoPortByte(EC_DATA_PORT, (byte)(Addr & 0xFF));

                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2E);
                WinRing.WriteIoPortByte(EC_DATA_PORT, 0x12);
                WinRing.WriteIoPortByte(EC_ADDR_PORT, 0x2F);
                buffer[i] = WinRing.ReadIoPortByte(EC_DATA_PORT);
            }
            return buffer;
        }

        public enum ITE_PORT : byte
        {
            EC_ADDR_PORT = 0x4E,
            EC_DATA_PORT = 0x4F,
        }
        public enum ITE_REGISTER_MAP : UInt16
        {
            ECINDAR0 = 0x103B, // Values from Smokeless Project, i kept them there but
            ECINDAR1 = 0x103C, // i'll use another set of addresses for fan control
            ECINDAR2 = 0x103D,
            ECINDAR3 = 0x103E,
            ECINDDR = 0x103F,
            GPDRA = 0x1601,
            GPCRA0 = 0x1610,
            GPCRA1 = 0x1611,
            GPCRA2 = 0x1612,
            GPCRA3 = 0x1613,
            GPCRA4 = 0x1614,
            GPCRA5 = 0x1615,
            GPCRA6 = 0x1616,
            GPCRA7 = 0x1617,
            GPOTA = 0x1671,
            GPDMRA = 0x1661,
            DCR0 = 0x1802,
            DCR1 = 0x1803,
            DCR2 = 0x1804,
            DCR3 = 0x1805,
            DCR4 = 0x1806,
            DCR5 = 0x1807,
            DCR6 = 0x1808,
            DCR7 = 0x1809,
            CTR2 = 0x1842,
            ECHIPID1 = 0x2000,
            ECHIPID2 = 0x2001,
            ECHIPVER = 0x2002,
            ECDEBUG = 0x2003,
            EADDR = 0x2100,
            EDAT = 0x2101,
            ECNT = 0x2102,
            ESTS = 0x2103,
            FW_VER = 0xC2C7, // Mapping out addresses from Smokeless' App, will use different enum for my purposes but here's what i found
            FAN_CUR_POINT = 0xC534, // Current point from fan curve selected by EC
            FAN_POINT = 0xC535, // Fan curve endpoint
            FAN1_BASE = 0xC540, // Fan1 Field Start addr
            FAN2_BASE = 0xC550, // Fan2 Field Start addr
            FAN_ACC_BASE = 0xC560, // Fan Accl Field addr
            FAN_DEC_BASE = 0xC570, // Fan Deccl Field addr
            CPU_TEMP = 0xC580, // Cpu ramp up addr field
            CPU_TEMP_HYST = 0xC590, // Cpu ramp down addr field
            GPU_TEMP = 0xC5A0, // Gpu ramp up addr field
            GPU_TEMP_HYST = 0xC5B0, // Gpu ramp down addr field
            VRM_TEMP = 0xC5C0, // Vrm/Hst ramp up addr field
            VRM_TEMP_HYST = 0xC5D0, // Vrm/Hst ramp down addr field
            CPU_TEMP_EN = 0xC631, // Unknown on gen 6 i get a 0 value
            GPU_TEMP_EN = 0xC632, // Unknown
            VRM_TEMP_EN = 0xC633, // Unknown
            FAN1_ACC_TIMER = 0xC3DA, // Didn't test
            FAN2_ACC_TIMER = 0xC3DB, // Didn't test
            FAN1_CUR_ACC = 0xC3DC, // Fan1 Accl gen5
            FAN1_CUR_DEC = 0xC3DD, // Fan1 Deccl gen5
            FAN2_CUR_ACC = 0xC3DE, // Fan2 Accl gen5
            FAN2_CUR_DEC = 0xC3DF, // Fan2 Deccl gen5
            FAN1_RPM_LSB = 0xC5E0, // Fan1 & Fan2 RPM LSB & MSB
            FAN1_RPM_MSB = 0xC5E1, // ^
            FAN2_RPM_LSB = 0xC5E2, // ^
            FAN2_RPM_MSB = 0xC5E3, // ^


            // Fan Control

            /*# Steps and Info required to set a Fan Curve

            # 1) Setting left & right fan acceleration and deceleration values
            # 2) Setting the number of points in the curve
            # 3) Setting left & right fan RPM values and number of points
            # 4) Setting temperature target points for when to ramp up or ramp down fans to a specific RPM for CPU, GPU and Heatsink for hysteresis
            # 5) (Optional for Gen6, required for Gen5 according to Underv0lti) Overriding Keyboard RGB Temp Trigger Value
            # 6) Set Fan speed change to be near instant as opposed to 100RPM every 5 seconds as Lenovo default for registering the new table*/

            //---------------------------------------------------------------------------------------------------------------------------------------

            // Step 1
            // FAN ACC/DEC Addresses -> This is where to set accl/decll speeds, values closer to 0 (in hex) will lead to instant changes but might
            // wear out fans. A value of 2 (2 seconds for example until it ramps up/down to the next fan step.) Use values between 1 and 0F(15).

            //---------------------------------------------------------------------------------------------------------------------------------------

            FAN1_ACC_GEN5 = 0xC3DC, // GEN 5 
            FAN1_DEC_GEN5 = 0xC3DD,
            FAN2_ACC_GEN5 = 0xC3DE,
            FAN2_DEC_GEN5 = 0xC3DF, // GEN 5 ACC/DEC VALUES

            FAN1_ACC_GEN6 = 0xC560, // GEN 6 ACC/DEC VALUES FROM 0 TO A (9 usable points, 10th hard stop point marker)
            FAN1_DEC_GEN6 = 0xC570,

            //---------------------------------------------------------------------------------------------------------------------------------------

            // Step 2
            // FAN Points used for the curve

            FAN_POINTS_NO = 0xC535, // 9 POINTS (a value of 0A) is the maximum, 10th point will be used by the EC, more points result in BSOD

            //---------------------------------------------------------------------------------------------------------------------------------------

            // Step 3
            // FAN RPM Addresses to write the RPM values to (HEX number 26 hex => 38 dec => 3800RPM)

            FAN1_RPM_ST_ADDR = 0xC551,
            FAN2_RPM_ST_ADDR = 0xC541,

            // For each point up to 0xC559 or 0xC549 depending on how many points are used
            // an rpm value must be specified like mentioned above.

            //---------------------------------------------------------------------------------------------------------------------------------------

            // Step 4
            // CPU RAMP UP AND RAMP DOWN Temperature THRESHOLDS

            CPU_RAMP_UP_THRS = 0xC580,
            CPU_RAMP_DOWN_THRS = 0xC591,

            // Starting address for ramp up last digit goes from 0 to 9
            // Starting address for ramp down last digit goes from 1 to 9. Ramp down
            // needs to be 1 value higher than ramp up and ramp up has to have it's
            // last digit equal to the last digit of the last point of ramp down and have
            // it's hex value equal to 7F which is used by Lenovo as the ignore value.
            // Same as before the value is a hex byte for example w 0xC580 37 would mean the first Ramp Up point activates at 55 degrees Celsius

            // GPU RAMP UP AND RAMP DOWN Temperature THRESHOLDS

            GPU_RAMP_UP_THRS = 0xC5A0,
            GPU_RAMP_DOWN_THRS = 0xC5B1,

            // HEATSINK RAMP UP AND RAMP DOWN Temperature THRESHOLDS

            HST_RAMP_UP_THRS = 0xC5C0,
            HST_RAMP_DOWN_THRS = 0xC5D1,

            // Set 7F(127) for ignoring values in the temperature threshold tables.

            //---------------------------------------------------------------------------------------------------------------------------------------

            // Step 5
            // Disable Fans from turning On when RGB keyboard is turned on

            STOP_RGB_FAN_WAKE = 0xC64D, // If this value is written to 25 it'll disable fans from turning on when RGB keyboard is turned on
            // at low temps/idling

            //---------------------------------------------------------------------------------------------------------------------------------------

            // Step 6
            // Set up fan counter registers to allow the change of fan tables faster internally
            // Setting to 64 for example will lead to a quick change

            FAN_TABLE_CHG_COUNTER = 0xC5FE,
            FAN_TABLE_CHG_COUNTER_SEC = 0xC5FF,

            //---------------------------------------------------------------------------------------------------------------------------------------
        }
    }

    enum DLL_Error_Code
    {
        OLS_DLL_NO_ERROR = 0,
        OLS_DLL_UNSUPPORTED_PLATFORM = 1,
        OLS_DLL_DRIVER_NOT_LOADED = 2,
        OLS_DLL_DRIVER_NOT_FOUND = 3,
        OLS_DLL_DRIVER_UNLOADED = 4,
        OLS_DLL_DRIVER_NOT_LOADED_ON_NETWORK = 5,
        OLS_DLL_UNKNOWN_ERROR = 9
    }

    enum DriverType
    {
        OLS_DRIVER_TYPE_UNKNOWN = 0,
        OLS_DRIVER_TYPE_WIN_9X = 1,
        OLS_DRIVER_TYPE_WIN_NT = 2,
        OLS_DRIVER_TYPE_WIN_NT4 = 3,
        OLS_DRIVER_TYPE_WIN_NT_X64 = 4,
        OLS_DRIVER_TYPE_WIN_NT_IA64 = 5
    }

    internal static class FanControl
    {
        private static void Main()
        {

            byte ecAddrPort = (byte)EC.ITE_PORT.EC_ADDR_PORT;
            byte ecDataPort = (byte)EC.ITE_PORT.EC_DATA_PORT;
            int powerModeWMI = 0;

            // Initialize WinRing
            WinRing.WinRingInitOk = WinRing.InitializeOls();
            // Check if WinRing initialized properly, if it did then Write & Read from the EC
            if (WinRing.WinRingInitOk)
            {


                try // Get Power Mode from WMI
                {
                    // Set up the WMI query
#pragma warning disable CA1416 // Validate platform compatibility
                    ManagementScope scope = new ManagementScope("\\\\.\\ROOT\\WMI");
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    ObjectQuery query = new ObjectQuery("SELECT * FROM LENOVO_GAMEZONE_DATA");
#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning disable CA1416 // Validate platform compatibility
                    ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
#pragma warning restore CA1416 // Validate platform compatibility

                    // Execute the query and retrieve the first result
#pragma warning disable CA1416 // Validate platform compatibility
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                    ManagementObject gameZoneData = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CA1416 // Validate platform compatibility

                    // Check if the result is not null
                    if (gameZoneData != null)
                    {
                        // Call the GetSmartFanMode method
#pragma warning disable CA1416 // Validate platform compatibility
                        ManagementBaseObject outParams = gameZoneData.InvokeMethod("GetSmartFanMode", null, null);
#pragma warning restore CA1416 // Validate platform compatibility

                        // Check if the outParams object is not null
                        if (outParams != null)
                        {
                            // Retrieve the 'Data' property from outParams
#pragma warning disable CA1416 // Validate platform compatibility
                            object dataProperty = outParams["Data"];
#pragma warning restore CA1416 // Validate platform compatibility

                            // Check if the 'Data' property is not null
                            if (dataProperty != null)
                            {
                                // Convert 'Data' property to the appropriate type (e.g., int) and display
                                powerModeWMI = Convert.ToInt32(dataProperty);
                                // Console.WriteLine($"Data Value: {powerModeWMI}"); // Debug purposes
                            }
                            else
                            {
                                Console.WriteLine("'Data' property is null.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("outParams is null.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("gameZoneData is null.");
                    }
                }
                catch (ManagementException ex)
                {
                    Console.WriteLine($"WMI Error: {ex.Message}");
                }

            }
            else
            {
                // Print a message indicating initialization failure
                Console.WriteLine("WinRing initialization failed. Check if the driver is loaded.");
            }


            // Deallocate info related to WinRing
            WinRing.DeinitializeOls();


            // Debug Section

            Console.WriteLine($"Power mode: {powerModeWMI}"); // Print Power Mode
            // Print EC_ADDR_PORT and EC_DATA_PORT values
            Console.WriteLine($"EC_ADDR_PORT: 0x{ecAddrPort:X}");
            Console.WriteLine($"EC_DATA_PORT: 0x{ecDataPort:X}");

            // Keep the console window open for user observation
            Console.ReadLine();
            //

        }
    }
}


// Info from an older .ps1 script i was using instead
// The previous script was using Smokeless PoC.exe paired with RwDrv.sys
// FE or FF00DXXX addresses map to 0xCXXX addresses in this C# app

/*    $current_power_profile = (Get - WmiObject -namespace ROOT\WMI -class LENOVO_GAMEZONE_DATA).GetSmartFanMode() | findstr -b "Data" | % { $_.replace("Data", "") } | % { $_.replace(":", "") } | % { $_.Trim() }
[-1] # Get Current Power Mode
    # 1 = Quiet, 2 = Balanced, 3 = Performance, 255 = Custom (Will be treated the same as Performance Mode)*/  // This was used in Powershell but i implemented it in C# in a different way

/*# Steps and Info required to set a Fan Curve

# 1) Setting left & right fan acceleration and deceleration values
# 2) Setting the number of points in the curve
# 3) Setting left & right fan RPM values and number of points
# 4) Setting temperature target points for when to ramp up or ramp down fans to a specific RPM for CPU, GPU and Heatsink for hysteresis
# 5) (Optional for Gen6, required for Gen5 according to Underv0lti) Overriding Keyboard RGB Temp Trigger Value
# 6) Set Fan speed change to be near instant as opposed to 100RPM every 5 seconds as Lenovo default for registering the new table*/

/*# Detailed Information

# Fan Addresses we're looking for in EC are marked with FF for Intel and FE for AMD, i'll provide examples for an AMD Legion5Pro Gen6(2021), so if you're on Intel replace FE with FF

# Step 1) 

# To set fans accel/dec values we have to write to the following addresses
# Left Fan Accel  : FE00D3DC        Left Fan Dec  : FE00D3DD
# Right Fan Accel : FE00D3DE        Right Fan Dec : FE00D3DF (This is for Gen5)
# For Gen6 we have to write the Accel and Deccel Values for each point
# Addresses are : FE00D560 to FE00D56A // Both Fan Acceleration FF00D570 to FF00D57A // Both fan Deceleration

# We have to write a value of 1 hex byte, for example 1, 2... 0F(15). The lower the value the faster it is, 1 is near instantenous, i recommend using 2, 3 or 4
# For example using PoC we need to write "w FE00D3DC 2".

# Step 2)

# We have to provide to the fan table the exact amount of Fan Points we will use otherwise it might incorectly use more, and/or because missing data it might get stuck or crash the EC leading to a reboot
# We need to write at the address FE00D535 a hex byte, for example if we use w FE00D535 06 we will have 5 points in our fan curve. You can't use more than 9 points otherwise it will crash your PC

# Step 3)

# Left Fan RPM Starting  address is  : FE00D551
# Right FAN RPM Starting address is  : FE00D541
# For each point FE00D551 ... up to FE00D559 (depending on how many points you use) you will do something like w FE00D551 26, The fan speed needs to be in a Hex value, for example 26(Hex) = 38(Dec) -> translated to 3800RPM 
# E.g w FE00D551 26 (using PoC)

# Step 4)

# CPU_Ramp_Up   FE00D580  Starting Address last digit goes from 0 to 9 
# CPU_Ramp_Down FE00D591  Starting Address last digit goes from 1 to 9, Ramp_Down needs to be 1 value higher than Ramp up and Ramp_Up has to have it's last digit equal to the last digit of the last point of Ramp_Down and have
# it's hex value equal to 7F which is used by lenovo as an ignore value. Same as before the value is a hex byte for example w FE00D580 37 would mean the first Ramp Up point activates at 55 degrees Celsius

# GPU_Ramp_Up   FE00D5A0   
# GPU_Ramp_Down FE00D5B1  

# Heatsink_Ramp_Up    FE00D5C0 
# Heatsink_Ramp_Down  FE00D5D1  Same principle as before

# Step 5)

# Write w FE00D64D 25 to disable Fans turning on When RGB keyboard is turned on

# Step 6)

# Set fan change counters to 100 to favour a faster fan table change (There is an internal counter that counts to 100 in 5 seconds by setting this to 100 we will change instantly)
# Write w FE00D5FE 64  and w FE00D5FF 64

# My curves will use the same temps for all the profiles, the only difference will be the FAN RPM between the 3 modes, and i will use 9 Points

# Example for my Gen6 Legion5ProAMD(2021)
# Number of points : 9
# Acceleration/Deceleration Values will be 2
# CPU Temps On       11 45 55 60 65 70 75 80 90
# Cpu Temps Off      10 43 53 58 63 68 73 78 87
# Gpu Temps On       11 50 55 60 63 66 69 72 75
# Gpu Temps Off      10 48 53 58 61 63 67 70 73
# Heatsink Temps On  11 50 55 65 70 75 80 85 90
# Heatsink Temps Off 10 48 53 63 68 73 78 83 85
# RPM Quiet              0 0 0 1800 2500 3200 3500 3800 4400       
# RPM Balanced           0 0 2200 3200 3500 3800 4100 4400 4700
# RPM Performance/Custom 0 0 2200 3600 3900 4200 4500 4800 5000*/

// Other info

/*# Technical Information provided by Underv0lti  This was based for using RwDrv.sys and PoC.exe(from smokelesscpu) FE/FF00DXXX from gen5, gen6 usually map out with succes to 0xCXXX using the WinRing and I/O Ports
// Implemented by Smokeless in this file

# Change the memory address based on year and CPU:
# FF00D520: Intel 2021/2020
# FE00D520: AMD 2021/2020
# FE0B0520: Intel/AMD 2022

# FF00D530: Mode indicator, changing it won't do anything useful.
# FF00D531: Indicates the fan curve is in the low temperature mode or not(changing it won't do anything useful, low temp curve: 1 normal curve: 2)
# FF00D534: Indicates the fan curve level the fans are currently at.
# FF00D535: Indicates the fan curve endpoint, can be changed to increase the fan curve length(the fans will ignore values that are placed after the endpoint)
# FF00D538: CPU Temperature
# FF00D539: GPU Temperature (can be disabled by setting 0xBD to 20 in the main EC or FF00D4BD)
# FF00D53A: Heatsink/VRM's Temperature
# FF00D5FC: CPU Fan Target RPM
# FF00D5FD: GPU Fan Target RPM
# FF00D4AB: Locks the fan speed and disables the fan delay counters at FF00D5FE/FF.

# Max Fan Curve Step Count: 9 Steps (10 might blue screen not advised to try)

# -------------------------------------

# FAN CONTROL

# -------------------------------------

# FF00D5E0&E1: CPU(Right) Fan TACH High and Low bits, read it in 16bit and you get the full fan RPM
# FF00D5E2&E3: GPU(Left) Fan TACH High and Low bits, read it in 16bit and you get the full fan RPM
# FF00D5E4: CPU(Right) Fan Instant Set Register(will get overwritten by the fan curve once the fan speed change delay ends) (fan delay bypassable by setting this register and the fan curve at the same time)
# FF00D5E5: CPU(Left) Fan Instant Set Register(will get overwritten by the fan curve once the fan speed change delay ends) (fan delay bypassable by setting this register and the fan curve at the same time)

# FF00D5FE: is the cpu fan delay counter, counts up to 100 in 5 seconds, you can bypass the fan delay if you set it to 64.
# FF00D5FF: is the cpu fan delay counter, counts up to 100 in 5 seconds, you can bypass the fan delay if you set it to 64.

# ----------------------------------------------------

# Fan Acceleration/Deceleration fix for 2020 models: 

# FF00D3DC: Fan 1 Acceleration
# FF00D3DD: Fan 2 Acceleration
# FF00D3DE: Fan 1 Deceleration
# FF00D3DF: Fan 2 Deceleration
# ----------------------------------------------------

# Fan Curve: 


# For CPU Fan, FF00D541 to whatever value you set at FF00D535 (e.g if you set 0A, the fan curve will be 9 steps max)
# For GPU Fan, FF00D551 to whatever value you set at FF00D535

# Fan Acceleration for both fan: FF00D561 to whatever value you set at FF00D535 (you can set different values for each steps in the curve)(doesn't work for some reason on 2020 models)
# Fan Deceleration for both fan: FF00D571 to whatever value you set at FF00D535 (you can set different values for each steps in the curve)(doesn't work for some reason on 2020 models)

# CPU Temperature Thresholds for Ramp Up: FF00D581 to whatever value you set at FF00D535
# CPU Temperature Thresholds for Hysteresis(Slow Down): FF00D591 to whatever value you set at FF00D535 

# GPU Temperature Thresholds for Ramp Up: FF00D5A1 to whatever value you set at FF00D535
# GPU Temperature Thresholds for Hysteresis(Slow Down): FF00D5B1 to whatever value you set at FF00D535

# VRM/Heatsink Temperature Thresholds for Ramp Up: FF00D5C1 to whatever value you set at FF00D535
# VRM/Heatsink Temperature Thresholds for Hysteresis(Slow Down): FF00D5D1 to whatever value you set at FF00D5D5

# Set 7F(127) for ignoring values in the temperature threshold tables.

# -------------------------------------------------------------------------------------------------*/


// ---------------


// Unused Code that was used during testing, left for educational purposes

/*// Assuming FAN1_RPM_LSB and FAN1_RPM_MSB are the addresses for FAN1 RPM
UInt16 fan1RpmLsbAddress = (UInt16)EC.ITE_REGISTER_MAP.FAN1_RPM_LSB;
UInt16 fan1RpmMsbAddress = (UInt16)EC.ITE_REGISTER_MAP.FAN1_RPM_MSB;

// Read the low byte and high byte of FAN1 RPM
byte fan1RpmLsb = EC.DirectECRead(ecAddrPort, ecDataPort, fan1RpmLsbAddress);
byte fan1RpmMsb = EC.DirectECRead(ecAddrPort, ecDataPort, fan1RpmMsbAddress); // correct

// Combine the low byte and high byte to get the FAN1 RPM
int fan1Rpm = (fan1RpmMsb << 8) | fan1RpmLsb;

// Display the FAN1 RPM
Console.WriteLine($"FAN1 RPM: {fan1Rpm} RPM");*/

// ---------------