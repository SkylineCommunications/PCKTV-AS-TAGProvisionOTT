/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

    Skyline Communications NV
    Ambachtenstraat 33
    B-8870 Izegem
    Belgium
    Tel.    : +32 51 31 35 69
    Fax.    : +32 51 31 01 29
    E-mail  : info@skyline.be
    Web     : www.skyline.be
    Contact : Ben Vandenberghe

****************************************************************************
Revision History:

DATE        VERSION     AUTHOR          COMMENTS

dd/mm/2023  1.0.0.1     XXX, Skyline    Initial version
****************************************************************************
*/

namespace Script
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Web;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
	using Skyline.DataMiner.ExceptionHelper;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Sections;
	using TagHelperMethods;

	/// <summary>
	/// DataMiner Script Class.
	/// </summary>
	public class Script
	{
		private DomHelper innerDomHelper;
		private ExceptionHelper exceptionHelper;

		/// <summary>
		/// The Script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			var scriptName = "PA_TAG_Monitor Scanner Progress";

			var helper = new PaProfileLoadDomHelper(engine);
			var domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");
			innerDomHelper = domHelper;
			exceptionHelper = new ExceptionHelper(engine, domHelper);
			var sharedMethods = new SharedMethods(engine, helper, domHelper);

			engine.GenerateInformation("START " + scriptName);

			var scanName = helper.GetParameterValue<string>("Scan Name (TAG Scan)");
			var instanceId = helper.GetParameterValue<string>("InstanceId (TAG Scan)");
			var tagElement = helper.GetParameterValue<string>("TAG Element (TAG Scan)");
			var instance = domHelper.DomInstances.Read(DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(instanceId)))).First();
			var status = instance.StatusId;

			if (!status.Equals("in_progress"))
			{
				helper.SendFinishMessageToTokenHandler();
				return;
			}

			try
			{
				var scanner = new Scanner
				{
					AssetId = helper.GetParameterValue<string>("Asset ID (TAG Scan)"),
					InstanceId = instanceId,
					ScanName = helper.GetParameterValue<string>("Scan Name (TAG Scan)"),
					SourceElement = helper.TryGetParameterValue("Source Element (TAG Scan)", out string sourceElement) ? sourceElement : String.Empty,
					SourceId = helper.TryGetParameterValue("Source ID (TAG Scan)", out string sourceId) ? sourceId : String.Empty,
					TagDevice = helper.GetParameterValue<string>("TAG Device (TAG Scan)"),
					TagElement = helper.GetParameterValue<string>("TAG Element (TAG Scan)"),
					TagInterface = helper.GetParameterValue<string>("TAG Interface (TAG Scan)"),
					ScanType = helper.GetParameterValue<string>("Scan Type (TAG Scan)"),
					Action = helper.GetParameterValue<string>("Action (TAG Scan)"),
					Channels = helper.TryGetParameterValue("Channels (TAG Scan)", out List<Guid> channels) ? channels : new List<Guid>(),
				};

				IDms dms = engine.GetDms();
				IDmsElement element = dms.GetElement(scanner.TagElement);

				var manifests = sharedMethods.GetManifests(instance);

				bool VerifyScan()
				{
					try
					{
						int iTotalExpected = 0;
						int iScanRequestChecked = 0;

						iTotalExpected = manifests.Count;

						object[][] scanChannelsRows = null;
						var scanChannelTable = element.GetTable(1310);
						scanChannelsRows = scanChannelTable.GetRows();

						if (scanChannelsRows == null)
						{
							return false;
						}

						iScanRequestChecked = ValidateScans(scanner, manifests, iScanRequestChecked, scanChannelsRows);

						if (iTotalExpected == iScanRequestChecked)
						{
							// done
							return true;
						}

						return false;
					}
					catch (Exception ex)
					{
						engine.GenerateInformation("Exception thrown while checking TAG Scan status: " + ex);
						throw;
					}
				}

				if (SharedMethods.Retry(VerifyScan, new TimeSpan(0, 5, 0)))
				{
					UpdateChannelLayoutPositions(engine, domHelper, scanner, element);

					sharedMethods.StartTAGChannelsProcess(scanner);
					helper.ReturnSuccess();
				}
				else
				{
					// failed to execute in time
					SharedMethods.TransitionToError(helper, status);
					var log = new Log
					{
						AffectedItem = tagElement,
						AffectedService = scanner.ScanName,
						Timestamp = DateTime.Now,
						LogNotes = "Failed to verify scan completion before timeout time",
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Severity = ErrorCode.SeverityType.Warning,
							Code = "RetryTimeout",
							Source = "Retry condition",
							Description = "Scan did not finish due to verify timeout.",
						},
					};
					exceptionHelper.GenerateLog(log);

					helper.SendFinishMessageToTokenHandler();
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation("Monitor scanner exception: " + ex);
				SharedMethods.TransitionToError(helper, status);
				var log = new Log
				{
					AffectedItem = tagElement,
					AffectedService = scanName,
					Timestamp = DateTime.Now,
					LogNotes = ex.ToString(),
					ErrorCode = new ErrorCode
					{
						ConfigurationItem = scriptName + " Script",
						ConfigurationType = ErrorCode.ConfigType.Automation,
						Severity = ErrorCode.SeverityType.Warning,
						Source = "Run()",
					},
				};
				exceptionHelper.ProcessException(ex, log);

				helper.SendFinishMessageToTokenHandler();
				throw;
			}
		}

		private void UpdateChannelLayoutPositions(IEngine engine, DomHelper domHelper, Scanner scanner, IDmsElement element)
		{
			try
			{
				var layoutUpdates = new Dictionary<string, List<LayoutUpdate>>();
				var allLayouts = element.GetTable(10300);
				foreach (var channel in scanner.Channels)
				{
					var channelFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(channel));
					var channelInstances = domHelper.DomInstances.Read(channelFilter);

					if (channelInstances.Count > 0)
					{
						var channelInstance = channelInstances.First();
						GetLayoutsToUpdate(channelInstance, layoutUpdates, allLayouts);
					}
					else
					{
						var log = new Log
						{
							AffectedItem = scanner.TagElement,
							AffectedService = scanner.ScanName,
							Timestamp = DateTime.Now,
							LogNotes = "Did not find any channel instances to update for the layout position.",
							ErrorCode = new ErrorCode
							{
								ConfigurationItem = "Monitor Scanner Progress Script",
								ConfigurationType = ErrorCode.ConfigType.Automation,
								Severity = ErrorCode.SeverityType.Minor,
								Code = "NoChannelInstances",
								Source = "UpdateChannelLayoutPositions()",
								Description = "Did not find any channel instances to update for the layout position.",
							},
						};

						exceptionHelper.GenerateLog(log);
					}
				}

				foreach (var update in layoutUpdates)
				{
					var layout = update.Key;
					var layoutsToUpdate = update.Value;
					var layoutFilter = new ColumnFilter { ComparisonOperator = ComparisonOperator.Equal, Value = layout, Pid = 10305 };
					var zeroFilter = new ColumnFilter { ComparisonOperator = ComparisonOperator.Equal, Value = "0", Pid = 10302 };
					var reservedFilter = new ColumnFilter { ComparisonOperator = ComparisonOperator.NotEqual, Value = "Reserved", Pid = 10303 };
					var layoutNoneRows = allLayouts.QueryData(new List<ColumnFilter> { layoutFilter, zeroFilter, reservedFilter }).ToList();
					if (layoutNoneRows.Any() && layoutNoneRows.Count() > update.Value.Count)
					{
						UpdateSequentialLayouts(update, layoutNoneRows, scanner);
					}
					else
					{
						var log = new Log
						{
							AffectedItem = scanner.TagElement,
							AffectedService = scanner.ScanName,
							Timestamp = DateTime.Now,
							LogNotes = $"Not enough free positions to set {layoutsToUpdate.Count} channels into {layout}",
							ErrorCode = new ErrorCode
							{
								ConfigurationItem = "Monitor Scanner Progress Script",
								ConfigurationType = ErrorCode.ConfigType.Automation,
								Severity = ErrorCode.SeverityType.Minor,
								Code = "NotEnoughSpaceInLayout",
								Source = "UpdateChannelLayoutPositions()",
								Description = "Insufficient space to insert channels into the given layout.",
							},
						};

						exceptionHelper.GenerateLog(log);
					}
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation("Failed to set channel layout positions due to exception: " + ex);
				var log = new Log
				{
					AffectedItem = scanner.TagElement,
					AffectedService = scanner.ScanName,
					Timestamp = DateTime.Now,
					LogNotes = ex.ToString(),
					ErrorCode = new ErrorCode
					{
						ConfigurationItem = "Monitor Scanner Progress Script",
						ConfigurationType = ErrorCode.ConfigType.Automation,
						Severity = ErrorCode.SeverityType.Minor,
						Source = "UpdateChannelLayoutPositions()",
					},
				};

				exceptionHelper.ProcessException(ex, log);
			}
		}

		private void UpdateSequentialLayouts(KeyValuePair<string, List<LayoutUpdate>> update, List<object[]> layoutNoneRows, Scanner scanner)
		{
			List<string> sequenceKeys = new List<string>();
			var expectedSequenceLength = update.Value.Count;

			sequenceKeys.Add(Convert.ToString(layoutNoneRows[0][0]));
			if (expectedSequenceLength != 1)
			{
				for (int i = 0; i < layoutNoneRows.Count - 2; i++)
				{
					var currentRow = layoutNoneRows[i];
					var nextRow = layoutNoneRows[i + 1];
					var currentSubKey = Convert.ToInt32(Convert.ToString(currentRow[0]).Split('/')[1]);
					var nextSubKey = Convert.ToInt32(Convert.ToString(nextRow[0]).Split('/')[1]);
					if (currentSubKey + 1 != nextSubKey)
					{
						sequenceKeys.Clear();
					}

					sequenceKeys.Add(Convert.ToString(nextRow[0]));
					if (sequenceKeys.Count >= expectedSequenceLength)
					{
						break;
					}
				}
			}

			if (sequenceKeys.Count < expectedSequenceLength)
			{
				// error, not enough layouts found in a row
				var log = new Log
				{
					AffectedItem = scanner.TagElement,
					AffectedService = scanner.ScanName,
					Timestamp = DateTime.Now,
					LogNotes = $"Not enough free positions in a row to set {expectedSequenceLength} channels into {update.Key}.",
					ErrorCode = new ErrorCode
					{
						ConfigurationItem = "Monitor Scanner Progress Script",
						ConfigurationType = ErrorCode.ConfigType.Automation,
						Severity = ErrorCode.SeverityType.Major,
						Code = "NotEnoughSequentialSpaceInLayout",
						Source = "UpdateSequentialLayouts()",
						Description = "Insufficient space to insert channels into the given layout where all channels will be together.",
					},
				};

				exceptionHelper.GenerateLog(log);
				return;
			}

			foreach (var layoutChannelUpdate in update.Value)
			{
				var positionKey = sequenceKeys.First();
				layoutChannelUpdate.UpdateChannelLayoutPosition(positionKey);
				sequenceKeys.Remove(positionKey);
			}
		}

		private void GetLayoutsToUpdate(DomInstance channelInstance, Dictionary<string, List<LayoutUpdate>> layoutUpdates, IDmsTable allLayouts)
		{
			foreach (var section in channelInstance.Sections)
			{
				Func<SectionDefinitionID, SectionDefinition> sectionDefinitionFunc = this.SetSectionDefinitionById;
				section.Stitch(sectionDefinitionFunc);

				var sectionDefinition = section.GetSectionDefinition();
				if (!sectionDefinition.GetName().Equals("Layouts"))
				{
					continue;
				}

				var fields = sectionDefinition.GetAllFieldDescriptors();
				var layoutField = section.GetFieldValueById(fields.First(x => x.Name.Contains("Layout Match")).ID);
				var layoutPositionField = fields.First(x => x.Name.Contains("Layout Position"));

				if (layoutField != null && !String.IsNullOrWhiteSpace(Convert.ToString(layoutField.Value.Value)))
				{
					// mark instance/section/field to be updated
					var layout = Convert.ToString(layoutField.Value.Value);
					if (!layoutUpdates.ContainsKey(layout))
					{
						layoutUpdates.Add(layout, new List<LayoutUpdate>());
					}

					layoutUpdates[layout].Add(new LayoutUpdate
					{
						AllLayouts = allLayouts,
						DomHelper = innerDomHelper,
						Channel = channelInstance,
						SectionToUpdate = sectionDefinition,
						LayoutPosition = layoutPositionField,
					});
				}
			}
		}

		private static int ValidateScans(Scanner scanner, List<Manifest> manifests, int iScanRequestChecked, object[][] scanChannelsRows)
		{
			foreach (var manifest in manifests)
			{
				foreach (var row in scanChannelsRows)
				{
					// Tried to refactor, but QueryData can't check for contains or a column equals two different values
					// Though ideally we can get around getting all rows in the table
					string[] urls = Convert.ToString(row[14]).Split('|');
					string title = HttpUtility.HtmlDecode(Convert.ToString(row[13]));
					var mode = (ModeState)Convert.ToInt32(row[2]);

					bool isScanFinished = mode == ModeState.Finished || mode == ModeState.FinishedRemoved;
					if (title.Contains(scanner.ScanName.Split(' ')[0]) && urls.Contains(manifest.Url) && isScanFinished)
					{
						iScanRequestChecked++;
						break;
					}
				}
			}

			return iScanRequestChecked;
		}

		private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
		{
			return this.innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
		}
	}

	public class LayoutUpdate
	{
		public IDmsTable AllLayouts { get; set; }

		public DomHelper DomHelper { get; set; }

		public DomInstance Channel { get; set; }

		public SectionDefinition SectionToUpdate { get; set; }

		public FieldDescriptor LayoutPosition { get; set; }

		public void UpdateChannelLayoutPosition(string position)
		{
			Channel.AddOrUpdateFieldValue(SectionToUpdate, LayoutPosition, position);
			AllLayouts.GetColumn<string>(10303).SetValue(position, "Reserved");
			DomHelper.DomInstances.Update(Channel);
		}
	}
}