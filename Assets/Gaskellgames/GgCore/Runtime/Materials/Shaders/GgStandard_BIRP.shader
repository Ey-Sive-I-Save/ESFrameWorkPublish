// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Gaskellgames/BIRP/GgStandard"
{
	Properties
	{
		[Header(Surface Inputs)][Space][Space][Toggle(_SCALEINDEPENDENTUV_ON)] _ScaleIndependentUV("Scale Independent UV", Float) = 1
		_BaseColor("Base Color", Color) = (1,1,1,0)
		_MainTex("Albedo", 2D) = "white" {}
		[Normal]_BumpMap("Normal Map", 2D) = "bump" {}
		_MaskMap("Mask (MSAoA)", 2D) = "white" {}
		_MetallicGloss("Metallic Gloss", Range( 0 , 1)) = 0
		_Smoothness("Smoothness", Range( 0 , 1)) = 0.3
		_AmbientOcclusion("Ambient Occlusion", Range( 0 , 1)) = 1
		[HDR][Header(Emission)][Space][Space]_EmissiveColor("Emissive Color", Color) = (0,0,0,0)
		[HDR]_EmissiveColorMap("Emissive", 2D) = "white" {}
		_EmissiveIntensity("Emissive Intensity", Range( 0 , 1)) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
		[Header(Forward Rendering Options)]
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 5.0
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#pragma shader_feature_local _SCALEINDEPENDENTUV_ON
		#define ASE_VERSION 19701
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows exclude_path:deferred dithercrossfade 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform sampler2D _BumpMap;
		uniform float4 _BumpMap_ST;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform half4 _BaseColor;
		uniform half4 _EmissiveColor;
		uniform half _EmissiveIntensity;
		uniform sampler2D _EmissiveColorMap;
		uniform float4 _EmissiveColorMap_ST;
		uniform half _MetallicGloss;
		uniform sampler2D _MaskMap;
		uniform float4 _MaskMap_ST;
		uniform half _Smoothness;
		uniform half _AmbientOcclusion;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float3 ase_worldNormal = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_vertexNormal = mul( unity_WorldToObject, float4( ase_worldNormal, 0 ) );
			ase_vertexNormal = normalize( ase_vertexNormal );
			float3 break3_g475 = ase_vertexNormal;
			float3 ase_parentObjectScale = (1.0/float3( length( unity_WorldToObject[ 0 ].xyz ), length( unity_WorldToObject[ 1 ].xyz ), length( unity_WorldToObject[ 2 ].xyz ) ));
			float2 uv_TexCoord10_g475 = i.uv_texcoord * ( _BumpMap_ST.xy * ( ( break3_g475.x * (ase_parentObjectScale).zy ) + ( break3_g475.y * (ase_parentObjectScale).xz ) + ( break3_g475.z * (ase_parentObjectScale).xy ) ) );
			#ifdef _SCALEINDEPENDENTUV_ON
				float2 staticSwitch4_g474 = uv_TexCoord10_g475;
			#else
				float2 staticSwitch4_g474 = ( i.uv_texcoord * _BumpMap_ST.xy );
			#endif
			o.Normal = UnpackNormal( tex2D( _BumpMap, ( staticSwitch4_g474 + _BumpMap_ST.zw ) ) );
			float3 break3_g472 = ase_vertexNormal;
			float2 uv_TexCoord10_g472 = i.uv_texcoord * ( _MainTex_ST.xy * ( ( break3_g472.x * (ase_parentObjectScale).zy ) + ( break3_g472.y * (ase_parentObjectScale).xz ) + ( break3_g472.z * (ase_parentObjectScale).xy ) ) );
			#ifdef _SCALEINDEPENDENTUV_ON
				float2 staticSwitch4_g471 = uv_TexCoord10_g472;
			#else
				float2 staticSwitch4_g471 = ( i.uv_texcoord * _MainTex_ST.xy );
			#endif
			float4 tex2DNode21_g464 = tex2D( _MainTex, ( staticSwitch4_g471 + _MainTex_ST.zw ) );
			float3 temp_output_51_0_g464 = ( (tex2DNode21_g464).rgb * (_BaseColor).rgb );
			o.Albedo = temp_output_51_0_g464;
			float3 temp_output_172_0_g464 = (_EmissiveColor).rgb;
			float3 temp_output_166_0_g464 = ( temp_output_172_0_g464 * _EmissiveIntensity );
			float3 BaseColor119_g464 = temp_output_51_0_g464;
			float3 break3_g466 = ase_vertexNormal;
			float2 uv_TexCoord10_g466 = i.uv_texcoord * ( _EmissiveColorMap_ST.xy * ( ( break3_g466.x * (ase_parentObjectScale).zy ) + ( break3_g466.y * (ase_parentObjectScale).xz ) + ( break3_g466.z * (ase_parentObjectScale).xy ) ) );
			#ifdef _SCALEINDEPENDENTUV_ON
				float2 staticSwitch4_g465 = uv_TexCoord10_g466;
			#else
				float2 staticSwitch4_g465 = ( i.uv_texcoord * _EmissiveColorMap_ST.xy );
			#endif
			float3 lerpResult169_g464 = lerp( temp_output_166_0_g464 , BaseColor119_g464 , ( 1.0 - (tex2D( _EmissiveColorMap, ( staticSwitch4_g465 + _EmissiveColorMap_ST.zw ) )).rgb ));
			o.Emission = lerpResult169_g464;
			float3 break3_g478 = ase_vertexNormal;
			float2 uv_TexCoord10_g478 = i.uv_texcoord * ( _MaskMap_ST.xy * ( ( break3_g478.x * (ase_parentObjectScale).zy ) + ( break3_g478.y * (ase_parentObjectScale).xz ) + ( break3_g478.z * (ase_parentObjectScale).xy ) ) );
			#ifdef _SCALEINDEPENDENTUV_ON
				float2 staticSwitch4_g477 = uv_TexCoord10_g478;
			#else
				float2 staticSwitch4_g477 = ( i.uv_texcoord * _MaskMap_ST.xy );
			#endif
			float4 tex2DNode269_g464 = tex2D( _MaskMap, ( staticSwitch4_g477 + _MaskMap_ST.zw ) );
			o.Metallic = ( _MetallicGloss * tex2DNode269_g464.r );
			o.Smoothness = ( tex2DNode269_g464.g * _Smoothness );
			o.Occlusion = ( saturate( tex2DNode269_g464.b ) * _AmbientOcclusion );
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"
}
/*ASEBEGIN
Version=19701
Node;AmplifyShaderEditor.FunctionNode;115;-256,-128;Inherit;False;StandardMat;0;;464;c1752ea365e9da74ca546a89027c3d92;4,205,0,188,0,110,0,174,1;0;11;FLOAT3;204;FLOAT3;202;FLOAT3;201;FLOAT;197;FLOAT3;200;FLOAT;198;FLOAT;199;FLOAT;195;FLOAT;194;FLOAT;196;FLOAT3;203
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;21;0,-128;Float;False;True;-1;7;;0;0;Standard;Gaskellgames/BIRP/GgStandard;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;True;True;True;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;ForwardOnly;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexScale;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;21;0;115;204
WireConnection;21;1;115;202
WireConnection;21;2;115;201
WireConnection;21;3;115;197
WireConnection;21;4;115;198
WireConnection;21;5;115;199
ASEEND*/
//CHKSM=743EBEB78EC6FC540B0AB1F34CCCE91C3F1AF47A