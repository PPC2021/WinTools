namespace KeepAlive;

public class TestInput
{
	public static SendInputClass.INPUT[] inputs = new SendInputClass.INPUT[1]
	{
		new SendInputClass.INPUT
		{
			type = 0u,
			u = new SendInputClass.InputUnion
			{
				mi = new SendInputClass.MouseInput
				{
					dx = 0,
					dy = 0,
					dwFlags = 1u,
					dwExtraInfo = SendInputClass.GetMessageExtraInfo()
				}
			}
		}
	};
}
