#include "stdafx.h"

#include "SADXModLoader.h"
#include "Trampoline.h"
#include <stack>

#include "HudScale.h"

// TODO: misc. 2D things (i.e lens flare), main menu, character select

#pragma region trampolines

static Trampoline* drawTrampoline;
static Trampoline* drawObjects;
static Trampoline* scaleRingLife;
static Trampoline* scaleScoreTime;
static Trampoline* scaleStageMission;
static Trampoline* scalePause;
static Trampoline* scaleTargetLifeGague;
static Trampoline* scaleScoreA;
static Trampoline* scaleTornadoHP;
static Trampoline* scaleTwinkleCircuitHUD;

#pragma endregion

#pragma region scale stack

enum class Align
{
	Auto,
	Left,
	Center,
	Right
};

static bool doScale = false;
static std::stack<Align> scale_stack;

static const float patch_dummy = 1.0f;

static float scale = 1.0f;
static float last_h = 0.0f;
static float last_v = 0.0f;

static void __cdecl ScalePush(Align align)
{
	scale_stack.push(align);

	if (doScale)
		return;

	last_h = HorizontalStretch;
	last_v = VerticalStretch;

	HorizontalStretch = 1.0f;
	VerticalStretch = 1.0f;

	doScale = true;
}

static void __cdecl ScalePop()
{
	scale_stack.pop();
	doScale = scale_stack.size() > 0;

	if (!doScale)
	{
		HorizontalStretch = last_h;
		VerticalStretch = last_v;
	}
}

#pragma endregion

FunctionPointer(void, ScoreDisplay_Main, (ObjectMaster*), 0x0042BCC0);
static void __cdecl ScaleResultScreen(ObjectMaster* _this)
{
	ScalePush(Align::Center);
	ScoreDisplay_Main(_this);
	ScalePop();
}

static void __cdecl DrawAllObjectsHax()
{
	if (doScale)
	{
		HorizontalStretch = last_h;
		VerticalStretch = last_v;
	}

	VoidFunc(original, drawObjects->Target());
	original();

	if (doScale)
	{
		HorizontalStretch = 1.0f;
		VerticalStretch = 1.0f;
	}
}

static void __cdecl ScaleA()
{
	ScalePush(Align::Left);
	VoidFunc(original, scaleRingLife->Target());
	original();
	ScalePop();
}

static void __cdecl ScaleB()
{
	ScalePush(Align::Left);
	VoidFunc(original, scaleScoreTime->Target());
	original();
	ScalePop();
}

static void __cdecl ScaleStageMission(ObjectMaster* _this)
{
	ScalePush(Align::Center);
	ObjectFunc(original, scaleStageMission->Target());
	original(_this);
	ScalePop();
}

static short __cdecl ScalePauseMenu()
{
	ScalePush(Align::Center);
	FunctionPointer(short, original, (void), scalePause->Target());
	short result = original();
	ScalePop();
	return result;
}

static void __cdecl ScaleTargetLifeGague(ObjectMaster* a1)
{
	ObjectFunc(original, scaleTargetLifeGague->Target());
	ScalePush(Align::Right);
	original(a1);
	ScalePop();
}

static void __cdecl ScaleScoreA()
{
	ScalePush(Align::Left);
	VoidFunc(original, scaleScoreA->Target());
	original();
	ScalePop();
}

static void __cdecl ScaleTornadoHP(ObjectMaster* a1)
{
	ScalePush(Align::Left);
	ObjectFunc(original, scaleTornadoHP->Target());
	original(a1);
	ScalePop();
}

static void __cdecl ScaleTwinkleCircuitHUD(ObjectMaster* a1)
{
	ScalePush(Align::Center);
	ObjectFunc(original, scaleTwinkleCircuitHUD->Target());
	original(a1);
	ScalePop();
}

static void __cdecl Draw2DSpriteHax(NJS_SPRITE* sp, Int n, Float pri, Uint32 attr, char zfunc_type)
{
	if (sp == nullptr)
		return;

	FunctionPointer(void, original, (NJS_SPRITE* sp, Int n, Float pri, Uint32 attr, char zfunc_type), drawTrampoline->Target());

	if (!doScale)
	{
		original(sp, n, pri, attr, zfunc_type);
	}
	else
	{
		NJS_POINT2 old_scale = { sp->sx, sp->sy };
		NJS_POINT3 old_pos = sp->p;

		sp->sx *= scale;
		sp->sy *= scale;
		sp->p.x *= scale;
		sp->p.y *= scale;

		Align top = scale_stack.top();

		if (top == Align::Auto)
		{
			static const float third = 640.0f / 3.0f;
			if (sp->p.x < third)
				top = Align::Left;
			else if (sp->p.x < third * 2.0f)
				top = Align::Center;
			else
				top = Align::Right;
		}

		switch (top)
		{
			default:
				break;

			case Align::Center:
				if ((float)HorizontalResolution / last_v > 640.0f)
					sp->p.x += (float)HorizontalResolution / 8.0f;
				if ((float)VerticalResolution / last_h > 480.0f)
					sp->p.y += (float)VerticalResolution / 8.0f;
				break;

			case Align::Right:
				if ((float)HorizontalResolution / last_v > 640.0f)
					sp->p.x += (float)HorizontalResolution / 4.0f;
				if ((float)VerticalResolution / last_h > 480.0f)
					sp->p.y += (float)VerticalResolution / 4.0f;
				break;
		}

		original(sp, n, pri, attr | NJD_SPRITE_SCALE, zfunc_type);

		sp->p = old_pos;
		sp->sx = old_scale.x;
		sp->sy = old_scale.y;
	}
}

void SetupHudScale()
{
	scale = min(HorizontalStretch, VerticalStretch);
	WriteJump((void*)0x0042BEE0, ScaleResultScreen);

	drawTrampoline = new Trampoline(0x00404660, 0x00404666, (DetourFunction)Draw2DSpriteHax);
	drawObjects = new Trampoline(0x0040B540, 0x0040B546, (DetourFunction)DrawAllObjectsHax);
	WriteCall((void*)((size_t)drawObjects->Target() + 1), (void*)0x004128F0);

	scaleRingLife = new Trampoline(0x00425F90, 0x00425F95, (DetourFunction)ScaleA);
	scaleScoreTime = new Trampoline(0x00427F50, 0x00427F55, (DetourFunction)ScaleB);
	scaleStageMission = new Trampoline(0x00457120, 0x00457126, (DetourFunction)ScaleStageMission);

	scalePause = new Trampoline(0x00415420, 0x00415425, (DetourFunction)ScalePauseMenu);
	WriteCall(scalePause->Target(), (void*)0x40FDC0);

	scaleTargetLifeGague = new Trampoline(0x004B3830, 0x004B3837, (DetourFunction)ScaleTargetLifeGague);

	scaleScoreA = new Trampoline(0x00628330, 0x00628335, (DetourFunction)ScaleScoreA);

	WriteData((const float**)0x006288C2, &patch_dummy);
	scaleTornadoHP = new Trampoline(0x00628490, 0x00628496, (DetourFunction)ScaleTornadoHP);

	scaleTwinkleCircuitHUD = new Trampoline(0x004DB5E0, 0x004DB5E5, (DetourFunction)ScaleTwinkleCircuitHUD);
	WriteCall(scaleTwinkleCircuitHUD->Target(), (void*)0x590620);
}
