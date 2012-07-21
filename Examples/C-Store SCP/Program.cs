﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Dicom;
using Dicom.Network;

using NLog;
using NLog.Config;
using NLog.Targets;

namespace Dicom.CStoreSCP {
	class Program {
		static string StoragePath = @".\DICOM";
		static string TempPath = @".\DICOM\Temp";

		static void Main(string[] args) {
			// initialize NLog logging
			var config = new LoggingConfiguration();

			var target = new ColoredConsoleTarget();
			target.Layout = "${message}";
			config.AddTarget("Console", target);
			config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, target));

			LogManager.Configuration = config;


			// preload dictionary to prevent timeouts
			var dict = DicomDictionary.Default;


			// start DICOM server on port 104
			var server = new DicomServer<CStoreSCP>(104);


			// end process
			Console.WriteLine("Press <return> to end...");
			Console.ReadLine();
		}

		class CStoreSCP : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider {
			private static DicomTransferSyntax[] AcceptedTransferSyntaxes = new DicomTransferSyntax[] {
				DicomTransferSyntax.ExplicitVRLittleEndian,
				DicomTransferSyntax.ExplicitVRBigEndian,
				DicomTransferSyntax.ImplicitVRLittleEndian
			};

			private static DicomTransferSyntax[] AcceptedImageTransferSyntaxes = new DicomTransferSyntax[] {
				// Lossless
				DicomTransferSyntax.JPEGLSLossless,
				DicomTransferSyntax.JPEG2000Lossless,
				DicomTransferSyntax.JPEGProcess14SV1,
				DicomTransferSyntax.JPEGProcess14,
				DicomTransferSyntax.RLELossless,
			
				// Lossy
				DicomTransferSyntax.JPEGLSNearLossless,
				DicomTransferSyntax.JPEG2000Lossy,
				DicomTransferSyntax.JPEGProcess1,
				DicomTransferSyntax.JPEGProcess2_4,

				// Uncompressed
				DicomTransferSyntax.ExplicitVRLittleEndian,
				DicomTransferSyntax.ExplicitVRBigEndian,
				DicomTransferSyntax.ImplicitVRLittleEndian
			};

			public CStoreSCP(Stream stream) : base(stream) {
			}

			public void OnReceiveAssociationRequest(DicomAssociation association) {
				foreach (var pc in association.PresentationContexts) {
					if (pc.AbstractSyntax == DicomUID.Verification)
						pc.AcceptTransferSyntaxes(AcceptedTransferSyntaxes);
					else
						pc.AcceptTransferSyntaxes(AcceptedImageTransferSyntaxes);
				}

				SendAssociationAccept(association);
			}

			public void OnReceiveAssociationReleaseRequest() {
				SendAssociationReleaseResponse();
			}

			public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason) {
			}

			public string GetTempFileName(DicomUID sopInstanceUid) {
				return Path.GetTempFileName();
			}

			public DicomCStoreResponse OnCStoreRequest(DicomCStoreRequest request) {
				var studyUid = request.Dataset.Get<string>(DicomTag.StudyInstanceUID);
				var instUid = request.SOPInstanceUID.UID;

				var path = Path.GetFullPath(Program.StoragePath);
				path = Path.Combine(path, studyUid);

				if (!Directory.Exists(path))
					Directory.CreateDirectory(path);

				path = Path.Combine(path, instUid) + ".dcm";

				request.File.Save(path);

				return new DicomCStoreResponse(request, DicomStatus.Success);
			}

			public DicomCEchoResponse OnCEchoRequest(DicomCEchoRequest request) {
				return new DicomCEchoResponse(request, DicomStatus.Success);
			}
		}
	}
}