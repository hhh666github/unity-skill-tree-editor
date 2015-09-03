﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace Adnc.SkillTree.Example.MultiCategory {
	public class SkillMenu : MonoBehaviour {
		Dictionary<SkillCollectionBase, SkillNode> nodeRef;
		List<SkillNode> skillNodes;

		public SkillTreeBase skillTree;

		[Header("Header")]
		[SerializeField] Transform categoryContainer;
		[SerializeField] GameObject categoryButtonPrefab;
		[SerializeField] Text skillOutput;
		[SerializeField] Text categoryName;

		[Header("Nodes")]
		[SerializeField] Transform nodeContainer;
		[SerializeField] GameObject nodeRowPrefab;
		[SerializeField] GameObject nodePrefab;
		[SerializeField] Color colorUnlock;
		[SerializeField] Color colorPurchase;
		[SerializeField] Color colorLock;

		[Header("Node Lines")]
		[SerializeField] Transform lineContainer;
		[SerializeField] GameObject linePrefab;
		[SerializeField] Color lineColor;

		[Header("Context Sidebar")]
		[SerializeField] RectTransform sidebarContainer;
		[SerializeField] Text sidebarTitle;
		[SerializeField] Text sidebarBody;
		[SerializeField] Text sidebarRequirements;
		[SerializeField] Text sidebarPurchasedMessage;
		[SerializeField] Button sidebarPurchase;

		void Start () {
			SkillCategoryBase[] skillCategories = skillTree.GetCategories();

			// Clear out test categories
			foreach (Transform child in categoryContainer) {
				Destroy(child.gameObject);
			}

			// Populate categories
			foreach (SkillCategoryBase category in skillCategories) {
				GameObject go = Instantiate(categoryButtonPrefab);
				go.transform.SetParent(categoryContainer);
				go.transform.localScale = Vector3.one;
				
				Text txt = go.GetComponentInChildren<Text>();
				txt.text = category.displayName;

				// Dump in a tmp variable to force capture the variable by the event
				SkillCategoryBase tmpCat = category; 
				go.GetComponent<Button>().onClick.RemoveAllListeners();
				go.GetComponent<Button>().onClick.AddListener(() => {
					ShowCategory(tmpCat);
				});
			}

			if (skillCategories.Length > 0) {
				ShowCategory(skillCategories[0]);
			}
		}

		public void ShowCategory (SkillCategoryBase category) {
			skillNodes = new List<SkillNode>();
			nodeRef = new Dictionary<SkillCollectionBase, SkillNode>();
			categoryName.text = string.Format("{0}: Level {1}", category.displayName, category.skillLv);
			ClearDetails();

			foreach (Transform child in nodeContainer) {
				Destroy(child.gameObject);
			}

			// Generate node row data
			List<List<SkillCollectionBase>> rows = new List<List<SkillCollectionBase>>();
			List<SkillCollectionBase> rootNodes = category.GetRootSkillCollections();
			rows.Add(rootNodes);

			Dictionary<SkillCollectionBase, bool> colHistory = new Dictionary<SkillCollectionBase, bool>();
			RecursiveRowAdd(rows, colHistory);

			// Output proper rows and attach data
			foreach (List<SkillCollectionBase> row in rows) {
				GameObject nodeRow = Instantiate(nodeRowPrefab);
				nodeRow.transform.SetParent(nodeContainer);
				nodeRow.transform.localScale = Vector3.one;
				
				foreach (SkillCollectionBase rowItem in row) {
					GameObject node = Instantiate(nodePrefab);
					node.transform.SetParent(nodeRow.transform);
					node.transform.localScale = Vector3.one;

					SkillNode skillNode = node.GetComponent<SkillNode>();
					skillNode.menu = this;
					skillNode.skillCollection = rowItem;
					skillNodes.Add(skillNode);

					nodeRef[rowItem] = skillNode;

					node.GetComponentInChildren<Text>().text = rowItem.displayName;
				}
			}

			StartCoroutine(ConnectNodes());
		}

		void UpdateNodes () {
			foreach (SkillNode node in skillNodes) {
				node.SetStatus(NodeStatus.Locked, colorLock);

				if (node.skillCollection.Skill.unlocked) {
					node.SetStatus(NodeStatus.Unlocked, colorUnlock);
				
				} else if (skillTree.skillPoints > 0 && node.skillCollection.Skill.IsRequirements()) {

					// Verify one parent node has at least one skill unlocked
					if (node.parents.Count > 0) {
						foreach (SkillNode parent in node.parents) {
							if (parent.skillCollection.GetSkill(0).unlocked) {
								node.SetStatus(NodeStatus.Purchasable, colorPurchase);
								break;
							}
						}
					} else {
						node.SetStatus(NodeStatus.Purchasable, colorPurchase);
					}
				}
			}
		}

		// Done after a frame skip so they nodes are sorted properly into position
		IEnumerator ConnectNodes () {
			bool skipFrame = true;

			if (skipFrame) {
				skipFrame = false;
				yield return null;
			}

			foreach (Transform line in lineContainer) {
				Destroy(line.gameObject);
			}

			// Generate lines between each node and populate parent / child relationships
			foreach (SkillNode node in skillNodes) {
				foreach (SkillCollectionBase child in node.skillCollection.childSkills) {
					node.children.Add(nodeRef[child]);
					nodeRef[child].parents.Add(node);
					DrawLine(lineContainer, node.transform.position, nodeRef[child].transform.position, lineColor);
				}
			}

			Repaint();
		}

		void DrawLine (Transform container, Vector3 start, Vector3 end, Color color) {
			GameObject go = Instantiate(linePrefab);
			go.transform.localScale = Vector3.one;

			go.GetComponent<Image>().color = color;

			// Adjust the layering so it appears underneath
			go.transform.SetParent(container);
			go.transform.SetSiblingIndex(0);

			// Adjust height to proper sizing
			RectTransform rectTrans = go.GetComponent<RectTransform>();
			Rect rect = rectTrans.rect;
			rect.height = Vector3.Distance(start, end);
			rectTrans.sizeDelta = new Vector2(rect.width, rect.height);

			// Adjust rotation and placement
			go.transform.rotation = Helper.Rotate2D(start, end);
			go.transform.position = start;
		}

		/// <summary>
		/// Recursively adds rows.
		/// </summary>
		/// <param name="rows">Rows.</param>
		/// <param name="history">History of all added items so we don't accidentally add two of the same thing</param>
		void RecursiveRowAdd (List<List<SkillCollectionBase>> rows, Dictionary<SkillCollectionBase, bool> history) {
			List<SkillCollectionBase> row = new List<SkillCollectionBase>();
			foreach (SkillCollectionBase collection in rows[rows.Count - 1]) {
				foreach (SkillCollectionBase child in collection.childSkills) {
					if (!row.Contains(child) && !history.ContainsKey(child)) {
						row.Add(child);
						history[child] = true;
					}
				}
			}

			if (row.Count > 0) {
				rows.Add(row);
				RecursiveRowAdd(rows, history);
			}
		}

		public void ShowNodeDetails (SkillNode node) {
			SkillCollectionBase skillCollection = node.skillCollection;
			NodeStatus status = node.GetStatus();

			sidebarTitle.text = string.Format("{0}: Lv {1}", skillCollection.displayName, skillCollection.SkillIndex + 1);
			sidebarBody.text = skillCollection.Skill.description;

			string requirements = skillCollection.Skill.GetRequirements();
			if (string.IsNullOrEmpty(requirements)) {
				sidebarRequirements.gameObject.SetActive(false);
			} else {
				sidebarRequirements.text = "<b>Requirements:</b> \n" + skillCollection.Skill.GetRequirements();
				sidebarRequirements.gameObject.SetActive(true);
			}

			if (status == NodeStatus.Purchasable) {
				sidebarPurchasedMessage.gameObject.SetActive(false);
				sidebarPurchase.gameObject.SetActive(true);
				sidebarPurchase.onClick.RemoveAllListeners();
				sidebarPurchase.onClick.AddListener(() => {
					skillCollection.Purchase();
					UpdateNodes();
					ShowNodeDetails(node);
					UpdateSkillPoints();
				});
			} else if (status == NodeStatus.Unlocked) {
				sidebarPurchasedMessage.gameObject.SetActive(true);
				sidebarPurchase.gameObject.SetActive(false);
			} else {
				sidebarPurchasedMessage.gameObject.SetActive(false);
				sidebarPurchase.gameObject.SetActive(false);
			}

			sidebarContainer.gameObject.SetActive(true);
		}

		void ClearDetails () {
			sidebarContainer.gameObject.SetActive(false);
		}

		void UpdateSkillPoints () {
			skillOutput.text = "Skill Points: " + skillTree.skillPoints;
		}

		void Repaint () {
			UpdateSkillPoints();
			UpdateNodes();
		}
	}
}
