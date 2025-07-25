namespace Drfisheye.Tuyable;

public enum TuyableCommandCode
{
	FUN_SENDER_DEVICE_INFO = 0x0000,
	FUN_SENDER_PAIR = 0x0001,
	FUN_SENDER_DPS = 0x0002,
	FUN_SENDER_DEVICE_STATUS = 0x0003,
	FUN_SENDER_UNBIND = 0x0005,
	FUN_SENDER_DEVICE_RESET = 0x0006,
	FUN_SENDER_OTA_START = 0x000C,
	FUN_SENDER_OTA_FILE = 0x000D,
	FUN_SENDER_OTA_OFFSET = 0x000E,
	FUN_SENDER_OTA_UPGRADE = 0x000F,
	FUN_SENDER_OTA_OVER = 0x0010,
	FUN_SENDER_DPS_V4 = 0x0027,
	FUN_RECEIVE_DP = 0x8001,
	FUN_RECEIVE_TIME_DP = 0x8003,
	FUN_RECEIVE_SIGN_DP = 0x8004,
	FUN_RECEIVE_SIGN_TIME_DP = 0x8005,
	FUN_RECEIVE_DP_V4 = 0x8006,
	FUN_RECEIVE_TIME_DP_V4 = 0x8007,
	FUN_RECEIVE_TIME1_REQ = 0x8011,
	FUN_RECEIVE_TIME2_REQ = 0x8012,
}