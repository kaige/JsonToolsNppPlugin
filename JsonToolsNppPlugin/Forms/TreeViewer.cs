﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using JSON_Tools.JSON_Tools;
using JSON_Tools.Utils;
using Kbg.NppPluginNET;

namespace JSON_Tools.Forms
{
    public partial class TreeViewer : Form
    {
        /// <summary>
        /// long treenode text causes HUGE performance problems, so set an arbitrary limit
        /// </summary>
        public const int MAX_TREENODE_JSON_LENGTH = 1024;

        /// <summary>
        /// the name of the file holding the JSON that this is associated with
        /// </summary>
        public string fname;

        /// <summary>
        /// Maps TreeNode.FullPath to the TreeNode's corresponding JNode
        /// </summary>
        public Dictionary<string, JNode> pathsToJNodes;

        /// <summary>
        /// result of latest RemesPath query
        /// </summary>
        public JNode queryResult;

        public JNode json;

        public RemesPathLexer lexer;

        public RemesParser remesParser;

        public JsonSchemaMaker schemaMaker;

        public FindReplaceForm findReplaceForm;

        /// <summary>
        /// if true, disables slow tree node selection actions
        /// </summary>
        private bool isExpandingAllSubtrees;

        /// <summary>
        /// If the user performs an undo or redo action,
        /// this will be set to true so that the next time the user performs a RemesPath query,
        /// the treeview is reset beforehand.
        /// </summary>
        public bool shouldRefresh;

        // event handlers for the node mouseclick drop down menu
        private static MouseEventHandler valToClipboardHandler = null;
        private static MouseEventHandler pathToClipboardHandler_Remespath = null;
        private static MouseEventHandler pathToClipboardHandler_Python = null;
        private static MouseEventHandler pathToClipboardHandler_Javascript = null;
        private static MouseEventHandler keyToClipboardHandler_Remespath = null;
        private static MouseEventHandler keyToClipboardHandler_Python = null;
        private static MouseEventHandler keyToClipboardHandler_Javascript = null;
        private static MouseEventHandler ToggleSubtreesHandler = null;
        private static MouseEventHandler selectThisHandler = null;
        private static MouseEventHandler showSortFormHandler = null;
        private static MouseEventHandler selectAllChildrenHandler = null;

        public TreeViewer(JNode json)
        {
            InitializeComponent();
            isExpandingAllSubtrees = false;
            pathsToJNodes = new Dictionary<string, JNode>();
            fname = Npp.notepad.GetCurrentFilePath();
            this.json = json;
            queryResult = json;
            remesParser = new RemesParser();
            lexer = new RemesPathLexer();
            findReplaceForm = null;
            FormStyle.ApplyStyle(this, Main.settings.use_npp_styling);
        }

        public bool UsesSelections()
        {
            if (!Main.TryGetInfoForFile(fname, out JsonFileInfo info))
                return false;
            return info.usesSelections;
        }

        /// <summary>
        /// suppress annoying ding when user hits a key in the tree view
        /// </summary>
        private void Tree_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Escape || e.KeyCode == Keys.Tab || e.KeyCode == Keys.Space)
                e.SuppressKeyPress = true;
        }

        // largely copied from NppManagedPluginDemo.cs in the original plugin pack
        private void TreeViewer_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || (e.KeyCode == Keys.Space && sender is TreeView))
            {
                // Ctrl+Enter in query box -> submit query
                if (e.Control && QueryBox.Focused)
                    SubmitQueryButton.PerformClick();
                else if (sender is Button btn)
                {
                    // Enter has the same effect as clicking a selected button
                    btn.PerformClick();
                }
                else if (sender is TreeView)
                {
                    // Enter or Space in the TreeView toggles children of the selected node
                    TreeNode selected = Tree.SelectedNode;
                    if (selected == null || selected.Nodes.Count == 0)
                        return;
                    if (selected.IsExpanded)
                        selected.Collapse(true); // don't collapse the children as well
                    else selected.Expand();
                }
            }
            // Escape -> go to editor
            else if (e.KeyData == Keys.Escape)
            {
                Npp.editor.GrabFocus();
            }
            // Tab -> go through controls, Shift+Tab -> go through controls backward
            else if (e.KeyCode == Keys.Tab)
            {
                Control next = GetNextControl((Control)sender, !e.Shift);
                while ((next == null) || (!next.TabStop)) next = GetNextControl(next, !e.Shift);
                next.Focus();
            }
        }

        private void QueryBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\t')
                e.Handled = true;
            // TODO: maybe add some way to highlight unclosed braces?
        }

        public void SetQueryBoxText(string text)
        {
            QueryBox.Text = text;
        }

        public static void SetImageOfTreeNode(TreeNode root, JNode json)
        {
            switch (json.type)
            {
                case Dtype.ARR: root.ImageIndex = 0; root.SelectedImageIndex = 0; break;
                case Dtype.BOOL: root.ImageIndex = 1; root.SelectedImageIndex = 1; break;
                case Dtype.DATE:
                case Dtype.DATETIME: root.ImageIndex = 2; root.SelectedImageIndex = 2; break;
                case Dtype.FLOAT: root.ImageIndex = 3; root.SelectedImageIndex = 3; break;
                case Dtype.INT: root.ImageIndex = 4; root.SelectedImageIndex = 4; break;
                case Dtype.OBJ: root.ImageIndex = 5; root.SelectedImageIndex = 5; break;
                case Dtype.STR: root.ImageIndex = 6; root.SelectedImageIndex = 6; break;
                default: root.ImageIndex = 7; root.SelectedImageIndex = 7; break;
            }
        }

        public void JsonTreePopulate(JNode json, TreeView tree = null)
        {
            if (tree == null) tree = this.Tree;
            if (json == null)
            {
                MessageBox.Show("Cannot populate the JSON tree because no JSON is stored.",
                    "Can't populate JSON tree",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }
            if (tree.ImageList == null && Main.settings.tree_node_images)
            {
                tree.ImageList = TypeIconList;
                /* type indices are as follows:
                0: array
                1: bool
                2: date or datetime
                3: float
                4: int
                5: object
                6: string
                7: everything else
                */
            }
            tree.Nodes.Clear();
            pathsToJNodes.Clear();
            TreeNode root = new TreeNode();
            if (Main.settings.tree_node_images)
                SetImageOfTreeNode(root, json);
            if (json is JArray arr)
            {
                root.Text = TextForTreeNode("JSON", json);
                if (arr.Length > 0)
                    root.Nodes.Add(""); // add the sentinel node
            }
            else if (json is JObject obj)
            {
                root.Text = TextForTreeNode("JSON", json);
                if (obj.Length > 0)
                    root.Nodes.Add("");
            }
            else
            {
                // just show the value for the scalar
                root.Text = JsonStringCappedLength(json);
            }
            tree.Nodes.Add(root);
            pathsToJNodes[root.FullPath] = json;
        }

        public static int IntervalBetweenJNodesWithTreeNodes(JNode json)
        {
            int interval = 0;
            if (json is JArray arr)
                interval = arr.Length / Main.settings.max_json_length_full_tree;
            else if (json is JObject obj)
                interval = obj.Length / Main.settings.max_json_length_full_tree;
            return interval < 1 ? 1 : interval;
        }

        /// <summary>
        /// Populate only the direct children of the root JNode into the TreeView.<br></br>
        /// Useful for very large JSON files where recursively populating all subtrees would<br></br>
        /// take an unacceptably long time and too much memory.<br></br>
        /// For JSON with more than some large number (default 10_000) children of the root node,
        /// only that many spaced children will be shown.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="json"></param>
        /// <param name="pathsToJNodes"></param>
        private static void JsonTreePopulateHelper_DirectChildren(TreeView tree,
                                                                  TreeNode root,
                                                                  JNode json,
                                                                  Dictionary<string, JNode> pathsToJNodes,
                                                                  bool usesSelections)
        {
            tree.BeginUpdate();
            try
            {
                int interval = IntervalBetweenJNodesWithTreeNodes(json);
                if (json is JArray arr)
                {
                    List<JNode> jar = arr.children;
                    for (int ii = 0; ii < jar.Count; ii += interval)
                    {
                        JNode child = jar[ii];
                        TreeNode child_node = root.Nodes.Add(TextForTreeNode(ii.ToString(), child));
                        if ((child is JArray childarr && childarr.Length > 0)
                            || (child is JObject childobj && childobj.Length > 0))
                        {
                            // add a sentinel node so that this node can later be expanded
                            child_node.Nodes.Add("");
                        }
                        if (Main.settings.tree_node_images)
                            SetImageOfTreeNode(child_node, child);
                        pathsToJNodes[child_node.FullPath] = child;
                    }
                }
                else if (json is JObject obj)
                {
                    Dictionary<string, JNode> jobj = obj.children;
                    int count = 0;
                    foreach (string key in SortRootKeysIfUsesSelections(root, jobj, usesSelections))
                    {
                        // iterate through keys with a stepsize of interval (step over some)
                        if (count++ % interval != 0)
                            continue;
                        JNode child = jobj[key];
                        TreeNode child_node = root.Nodes.Add(key, TextForTreeNode(key, child));
                        if ((child is JArray childarr && childarr.Length > 0)
                            || (child is JObject childobj && childobj.Length > 0))
                        {
                            child_node.Nodes.Add("");
                        }
                        if (Main.settings.tree_node_images)
                            SetImageOfTreeNode(child_node, child);
                        pathsToJNodes[child_node.FullPath] = child;
                    }
                }
            }
            catch (Exception ex)
            {
                string expretty = RemesParser.PrettifyException(ex);
                MessageBox.Show($"Could not populate JSON tree because of error:\n{expretty}",
                                "Error while populating tree",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
            tree.EndUpdate();
        }

        /// <summary>
        /// if root is the root of the entire treeview and usesSelections and the root JSON is an object,
        /// correctly orders the keys of dict according to the start position of each selection
        /// (e.g., ["3,5","11,15","22,30"])<br></br>
        /// otherwise, returns the keys of dict in their natural order
        /// </summary>
        private static IEnumerable<string> SortRootKeysIfUsesSelections(TreeNode node, Dictionary<string, JNode> dict, bool usesSelections)
        {
            if (usesSelections && node.Parent == null && dict.Keys.All(SelectionManager.IsStartEnd))
            {
                string[] keys = dict.Keys.ToArray();
                Array.Sort(keys, SelectionManager.StartEndCompareByStart);
                return keys;
            }
            else
                return dict.Keys;
        }

        /// <summary>node.ToString() capped at MAX_TREENODE_JSON_LENGTH </summary>
        private static string JsonStringCappedLength(JNode node)
        {
            string jsonstr = node.ToString();
            if (jsonstr.Length > MAX_TREENODE_JSON_LENGTH)
                return jsonstr.Substring(0, MAX_TREENODE_JSON_LENGTH) + "...";
            return jsonstr;
        }

        private static string TextForTreeNode(string key, JNode node)
        {
            if (node is JArray arr)
                return arr.children.Count == 0
                    ? $"{key} : []"
                    : $"{key} : [{arr.children.Count}]";
            if (node is JObject obj)
                return obj.children.Count == 0
                    ? $"{key} : {{}}"
                    : $"{key} : {{{obj.children.Count}}}";
            return $"{key} : {JsonStringCappedLength(node)}";
        }

        private void SubmitQueryButton_Click(object sender, EventArgs e)
        {
            if (json == null) return;
            if (shouldRefresh)
                RefreshButton.PerformClick();
            bool usesSelections = UsesSelections();
            string query = QueryBox.Text;
            JNode queryFunc;
            // complex queries may mutate the input JSON but return only a subset of the JSON.
            // in this case, we want the tree to be populated by the subset returned by query_func.Operate (because the point of the tree is to provide a view into subsets of the input)
            // but we want the document to be repopulated with the original JSON (after its mutation by the complex query)
            JNode treeFunc;
            try
            {
                queryFunc = remesParser.Compile(query);
            }
            catch (Exception ex)
            {
                string expretty = RemesParser.PrettifyException(ex);
                MessageBox.Show($"Could not execute query {query} because of compilation error:\n{expretty}",
                                "Compilation error in RemesPath query",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                return;
            }
            int runtimeErrorMessagesShown = 0;
            bool suppressRuntimeErrorMsg = false;
            void RuntimeErrorMessage(Exception ex, string selectionStartEnd = null)
            {
                if (!suppressRuntimeErrorMsg
                    && ++runtimeErrorMessagesShown % 5 == 0
                    && MessageBox.Show("Select Yes to stop seeing error message boxes for this query",
                        "Stop seeing errors?",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question
                       ) == DialogResult.Yes)
                    suppressRuntimeErrorMsg = true;
                if (suppressRuntimeErrorMsg)
                    return;
                string expretty = RemesParser.PrettifyException(ex);
                string errorMessage;
                if (selectionStartEnd != null && SelectionManager.IsStartEnd(selectionStartEnd))
                {
                    int[] startEnd = SelectionManager.ParseStartEnd(selectionStartEnd);
                    int start = startEnd[0]; int end = startEnd[1];
                    errorMessage = $"While executing query {query} on selection between positions {start} and {end}, encountered runtime error:\n{expretty}";
                }
                else
                    errorMessage = $"While executing query {query}, encountered runtime error:\n{expretty}";
                MessageBox.Show(errorMessage,
                                "Runtime error while executing query",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            };
            // if the query mutates input, we need to overwrite the file with the
            // modified JSON after the query has been executed
            if (queryFunc.IsMutator)
            {
                bool isMultiStepQuery = queryFunc is JQueryContext;
                if (usesSelections)
                {
                    // in order to track which query results are in which selections,
                    // each query needs to be performed separately on each selection
                    var obj = (JObject)json;
                    var queryObj = new JObject();
                    var treeObj = isMultiStepQuery ? new JObject() : queryObj;
                    foreach (KeyValuePair<string, JNode> kv in obj.children)
                    {
                        try
                        {
                            var subquery_func = queryFunc.Operate(kv.Value);
                            if (isMultiStepQuery)
                            {
                                treeObj[kv.Key] = subquery_func;
                                queryObj[kv.Key] = kv.Value;
                            }
                            else
                                queryObj[kv.Key] = subquery_func;
                        }
                        catch (Exception ex)
                        {
                            RuntimeErrorMessage(ex, kv.Key);
                        }
                    }
                    treeFunc = treeObj;
                    queryFunc = queryObj;
                }
                else
                {
                    try
                    {
                        JNode result = queryFunc.Operate(json);
                        treeFunc = result;
                        queryFunc = isMultiStepQuery ? json : result;
                    }
                    catch (Exception ex)
                    {
                        RuntimeErrorMessage(ex);
                        return;
                    }
                }
                Dictionary<string, (string, JNode)> keyChanges = Main.ReformatFileWithJson(queryFunc, Main.PrettyPrintFromSettings, usesSelections);
                if (isMultiStepQuery && usesSelections && treeFunc is JObject treeObj_)
                {
                    var treeKeyChanges = new Dictionary<string, (string, JNode)>();
                    foreach (string changedKey in keyChanges.Keys)
                    {
                        string newKey = keyChanges[changedKey].Item1;
                        if (treeObj_.children.TryGetValue(changedKey, out JNode changedKeyVal))
                            treeKeyChanges[changedKey] = (newKey, changedKeyVal);
                    }
                    Main.RenameAll(treeKeyChanges, treeObj_);
                }
                json = queryFunc;
                Main.jsonFileInfos[fname].json = queryFunc;
                queryResult = treeFunc;
            }
            // not an assignment expression, so executing the query changes the contents of the tree
            // but leaves the text of the document unchanged
            else if (queryFunc.CanOperate)
            {
                if (usesSelections)
                {
                    var obj = (JObject)json;
                    var queryObj = new JObject();
                    foreach (KeyValuePair<string, JNode> kv in obj.children)
                    {
                        try
                        {
                            JNode subquery_result = queryFunc.Operate(kv.Value);
                            queryObj[kv.Key] = subquery_result;
                        }
                        catch (Exception ex)
                        {
                            RuntimeErrorMessage(ex, kv.Key);
                        }
                    }
                    queryResult = queryObj;
                }
                else
                {
                    try
                    {
                        queryResult = queryFunc.Operate(json);
                    }
                    catch (Exception ex)
                    {
                        RuntimeErrorMessage(ex);
                        return;
                    }
                }
                treeFunc = queryResult;
            }
            else
            {
                // query_func is a constant, so just set the query to that
                queryResult = queryFunc;
                treeFunc = queryResult;
            }
            JsonTreePopulate(treeFunc);
        }

        private void QueryToCsvButton_Click(object sender, EventArgs e)
        {
            if (queryResult == null) return;
            using (var jsonToCsv = new JsonToCsvForm(queryResult))
                jsonToCsv.ShowDialog();
        }

        private void SaveQueryResultButton_Click(object sender, EventArgs e)
        {
            if (queryResult == null) return;
            Main.PrettyPrintJsonInNewFile(queryResult);
        }

        private void FindReplaceButton_Click(object sender, EventArgs e)
        {
            if (findReplaceForm == null || findReplaceForm.IsDisposed)
            {
                RemoveOwnedForm(findReplaceForm);
                findReplaceForm = new FindReplaceForm(this);
                AddOwnedForm(findReplaceForm);
                findReplaceForm.Show();
            }
            else findReplaceForm.Focus();
        }

        /// <summary>
        /// Snap the caret to the position of the JNode corresponding to the TreeNode selected.<br></br>
        /// Also populate the current path box with the path to the selected node.<br></br>
        /// This happens when a node is clicked, expanded, or selected by arrow keys
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tree_NodeSelection(object sender, EventArgs e)
        {
            if (Npp.notepad.GetCurrentFilePath() != fname) return;
            TreeNode node;
            try
            {
                node = (TreeNode)e.GetType().GetProperty("Node").GetValue(e, null);
            }
            catch { return; }
            if (node == null) return;
            Npp.editor.GotoPos(NodePosInJsonDoc(node));
            // might also want to make it so that the selected line is scrolled to the top
            CurrentPathBox.Text = PathToTreeNode(node, Main.settings.key_style);
        }

        /// <summary>
        /// For TreeViews of multi-selection documents, each selection has its own sub-tree<br></br>
        /// associated with the key "{selection start},{selection end}"<br></br>
        /// To determine which selection a TreeNode belongs to, we recursively search the parent hierarchy of node
        /// until we find a node whose text has a key of that form,<br></br>
        /// then we return {selection start} from the key "{selection start},{selection end}"<br></br>
        /// For a whole-document treeview, return 0.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private int ParentSelectionStartPos(TreeNode node)
        {
            if (!UsesSelections())
                return 0;
            int start = 0;
            while (node.Parent != null)
            {
                Match mtch = Regex.Match(node.Text, @"^(\d+),\d+ : ");
                if (mtch.Success)
                {
                    start = int.Parse(mtch.Groups[1].Value);
                    break;
                }
                node = node.Parent;
            }
            return start;
        }

        /// <summary>
        /// position of associated JNode, after factoring in the position of the JNode's parent selection
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private int NodePosInJsonDoc(TreeNode node)
        {
            return pathsToJNodes[node.FullPath].position + ParentSelectionStartPos(node);
        }

        private void Tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            Tree_NodeSelection(sender, e);
        }

        private void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (isExpandingAllSubtrees) return;
            // all this is slow and unnecessary when expanding all subtrees
            ReplaceSentinelWithChildren(Tree, e.Node);
            Tree_NodeSelection(sender, e);
        }

        /// <summary>
        /// We use a sentinel node to indicate that a node's corresponding JNode
        /// has children that have not yet been associated with their own tree nodes.<br></br>
        /// This mechanism improves performance by allowing us to lazily construct the tree on demand.
        /// </summary>
        private static bool HasSentinelChild(TreeNode node)
        {
            return node.Nodes.Count == 1 && node.Nodes[0].Text.Length == 0;
        }

        private void ReplaceSentinelWithChildren(TreeView tree, TreeNode node)
        {
            var nodes = node.Nodes;
            if (HasSentinelChild(node))
            {
                nodes.RemoveAt(0);
                JNode jnode = pathsToJNodes[node.FullPath];
                JsonTreePopulateHelper_DirectChildren(tree, node, jnode, pathsToJNodes, UsesSelections());
            }
        }

        /// <summary>
        /// Populate the full subtree of the root JNode into the TreeView.<br></br>
        /// This algorithm is very slow, but when we *know* that the user wants to recursively
        /// populate the full subtree (when they click the Expand/Collapse all subtrees button)
        /// it is still much faster than JsonTreePopulateHelper_DirectChildren followed by ReplaceSentinelWithChildren.<br></br>
        /// For JSON with more than some large number (default 10_000) children of the root node,
        /// only that many spaced children will be shown.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="json"></param>
        /// <param name="pathsToJNodes"></param>
        private static void JsonTreePopulate_FullRecursive(TreeView tree,
                                                           TreeNode root,
                                                           JNode json,
                                                           Dictionary<string, JNode> pathsToJNodes,
                                                           bool usesSelections)
        {
            int interval = IntervalBetweenJNodesWithTreeNodes(json);
            if (HasSentinelChild(root))
                root.Nodes.RemoveAt(0);
            if (root.Nodes.Count == 0)
            // only populate the children for tree nodes that don't yet have children
            {
                try
                {
                    if (json is JArray arr_)
                    {
                        List<JNode> jar = arr_.children;
                        for (int ii = 0; ii < jar.Count; ii += interval)
                        {
                            JNode child = jar[ii];
                            TreeNode child_node = root.Nodes.Add(TextForTreeNode(ii.ToString(), child));
                            if (Main.settings.tree_node_images)
                                SetImageOfTreeNode(child_node, child);
                            pathsToJNodes[child_node.FullPath] = child;
                        }
                    }
                    else if (json is JObject obj_)
                    {
                        Dictionary<string, JNode> jobj = obj_.children;
                        int count = 0;
                        foreach (string key in SortRootKeysIfUsesSelections(root, jobj, usesSelections))
                        {
                            // iterate through keys with a stepsize of interval (step over some)
                            if (count++ % interval != 0)
                                continue;
                            JNode child = jobj[key];
                            TreeNode child_node = root.Nodes.Add(key, TextForTreeNode(key, child));
                            if (Main.settings.tree_node_images)
                                SetImageOfTreeNode(child_node, child);
                            pathsToJNodes[child_node.FullPath] = child;
                        }
                    }
                }
                catch (Exception ex)
                {
                    string expretty = RemesParser.PrettifyException(ex);
                    MessageBox.Show($"Could not populate JSON tree because of error:\n{expretty}",
                                    "Error while populating tree",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                }
            }
            // now this node has its direct children populated, so continue the recursion
            if (json is JArray arr)
            {
                List<JNode> jar = arr.children;
                for (int ii = 0; ii < root.Nodes.Count; ii++)
                {
                    TreeNode child_node = root.Nodes[ii];
                    JNode child = jar[ii * interval];
                    if ((child is JArray childarr && childarr.Length > 0)
                        || (child is JObject childobj && childobj.Length > 0))
                    {
                        JsonTreePopulate_FullRecursive(tree, child_node, child, pathsToJNodes, false);
                    }
                }
            }
            else if (json is JObject obj)
            {
                Dictionary<string, JNode> jobj = obj.children;
                for (int ii = 0; ii < root.Nodes.Count; ii++)
                {
                    TreeNode child_node = root.Nodes[ii];
                    string key = child_node.Name;
                    JNode child = jobj[key];
                    if ((child is JArray childarr && childarr.Length > 0)
                        || (child is JObject childobj && childobj.Length > 0))
                    {
                        JsonTreePopulate_FullRecursive(tree, child_node, child, pathsToJNodes, false);
                    }
                }
            }
        }

        /// <summary>
        /// On right click, throw up a context menu that lets you do the following:<br></br>
        /// - Copy the current node's value to the clipboard<br></br>
        /// - Copy the node's path (Python style) to the clipboard<br></br>
        /// - Copy the node's key/index (Python style) to the clipboard<br></br>
        /// - Copy the node's path (JavaScript style) to the clipboard<br></br>
        /// - Copy the node's path (RemesPath style) to the clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode node = e.Node;
            if (e.Button == MouseButtons.Right)
            {
                // open a context menu that lets the user copy things to clipboard
                // lets user copy string value of current node to clipboard
                var valToClipboard = NodeRightClickMenu.Items[0];
                if (valToClipboardHandler != null)
                {
                    try
                    {
                        valToClipboard.MouseUp -= valToClipboardHandler;
                    }
                    catch { }
                }
                valToClipboardHandler = new MouseEventHandler(
                    (object sender2, MouseEventArgs e2) =>
                    {
                        JNode jnode = pathsToJNodes[node.FullPath];
                        if (jnode is JObject || jnode is JArray)
                            return;
                        Npp.TryCopyToClipboard(jnode.ToString());
                    }
                );
                valToClipboard.MouseUp += valToClipboardHandler;
                // things that get the key of the current node to clipboard
                var keyToClipboard = (ToolStripMenuItem)NodeRightClickMenu.Items[1];
                var keyToClipboard_javascript = keyToClipboard.DropDownItems[0];
                if (keyToClipboardHandler_Javascript != null)
                {
                    try
                    {
                        keyToClipboard_javascript.MouseUp -= keyToClipboardHandler_Javascript;
                    }
                    catch { }
                }
                keyToClipboardHandler_Javascript = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        Npp.TryCopyToClipboard(KeyOfTreeNode(node, KeyStyle.JavaScript));
                    }
                );
                keyToClipboard_javascript.MouseUp += keyToClipboardHandler_Javascript;
                var keyToClipboard_Python = keyToClipboard.DropDownItems[1];
                if (keyToClipboardHandler_Python != null)
                {
                    try
                    {
                        keyToClipboard_Python.MouseUp -= keyToClipboardHandler_Python;
                    }
                    catch { }
                }
                keyToClipboardHandler_Python = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        Npp.TryCopyToClipboard(KeyOfTreeNode(node, KeyStyle.Python));
                    }
                );
                keyToClipboard_Python.MouseUp += keyToClipboardHandler_Python;
                var keyToClipboard_RemesPath = keyToClipboard.DropDownItems[2];
                if (keyToClipboardHandler_Remespath != null)
                {
                    try
                    {
                        keyToClipboard_RemesPath.MouseUp -= keyToClipboardHandler_Remespath;
                    }
                    catch { }
                }
                keyToClipboardHandler_Remespath = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        Npp.TryCopyToClipboard(KeyOfTreeNode(node, KeyStyle.RemesPath));
                    }
                );
                keyToClipboard_RemesPath.MouseUp += keyToClipboardHandler_Remespath;
                // drop down menu for getting path to clipboard
                var pathToClipboard = (ToolStripMenuItem)NodeRightClickMenu.Items[2];
                var pathToClipboard_javascript = pathToClipboard.DropDownItems[0];
                if (pathToClipboardHandler_Javascript != null)
                {
                    try
                    {
                        pathToClipboard_javascript.MouseUp -= pathToClipboardHandler_Javascript;
                    }
                    catch { }
                }
                pathToClipboardHandler_Javascript = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        Npp.TryCopyToClipboard(PathToTreeNode(node, KeyStyle.JavaScript));
                    }
                );
                pathToClipboard_javascript.MouseUp += pathToClipboardHandler_Javascript;
                var pathToClipboard_Python = pathToClipboard.DropDownItems[1];
                if (pathToClipboardHandler_Python != null)
                {
                    try
                    {
                        pathToClipboard_Python.MouseUp -= pathToClipboardHandler_Python;
                    }
                    catch { }
                }
                pathToClipboardHandler_Python = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        Npp.TryCopyToClipboard(PathToTreeNode(node, KeyStyle.Python));
                    }
                );
                pathToClipboard_Python.MouseUp += pathToClipboardHandler_Python;
                var pathToClipboard_RemesPath = pathToClipboard.DropDownItems[2];
                if (pathToClipboardHandler_Remespath != null)
                {
                    try
                    {
                        pathToClipboard_RemesPath.MouseUp -= pathToClipboardHandler_Remespath;
                    }
                    catch { }
                }
                pathToClipboardHandler_Remespath = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        Npp.TryCopyToClipboard(PathToTreeNode(node, KeyStyle.RemesPath));
                    }
                );
                pathToClipboard_RemesPath.MouseUp += pathToClipboardHandler_Remespath;
                switch (Main.settings.key_style)
                {
                    case (KeyStyle.RemesPath): pathToClipboard.MouseUp += pathToClipboardHandler_Remespath; break;
                    case (KeyStyle.Python): pathToClipboard.MouseUp += pathToClipboardHandler_Python; break;
                    case (KeyStyle.JavaScript): pathToClipboard.MouseUp += pathToClipboardHandler_Javascript; break;
                }
                NodeRightClickMenu.Items[3].MouseUp -= ToggleSubtreesHandler;
                JNode nodeJson = pathsToJNodes[node.FullPath];
                ToggleSubtreesHandler = new MouseEventHandler(
                    (s2, e2) =>
                    {
                        if (node.Nodes.Count > 0)
                        {
                            if (node.IsExpanded)
                                node.Collapse();
                            else
                            {
                                // node.ExpandAll() is VERY VERY SLOW if we don't do it this way
                                Tree.BeginUpdate();
                                isExpandingAllSubtrees = true;
                                JsonTreePopulate_FullRecursive(Tree, node, nodeJson, pathsToJNodes, UsesSelections());
                                node.ExpandAll();
                                isExpandingAllSubtrees = false;
                                Tree.EndUpdate();
                            }
                        }
                    }
                );
                NodeRightClickMenu.Items[3].MouseUp += ToggleSubtreesHandler;
                var selectThisItem = NodeRightClickMenu.Items[4];
                selectThisItem.MouseUp -= selectThisHandler;
                selectThisHandler = new MouseEventHandler(
                    (s2, e2) => SelectTreeNodeJson(node)
                );
                selectThisItem.MouseUp += selectThisHandler;
                var sortFormOpenItem = NodeRightClickMenu.Items[5];
                var selectAllChildrenItem = NodeRightClickMenu.Items[6];
                if (nodeJson is JArray || nodeJson is JObject)
                {
                    sortFormOpenItem.Visible = true;
                    sortFormOpenItem.MouseUp -= showSortFormHandler;
                    showSortFormHandler = new MouseEventHandler(
                        (s2, e2) =>
                        {
                            Main.sortForm = new SortForm();
                            string path = PathToTreeNode(node, KeyStyle.RemesPath);
                            Main.sortForm.PathTextBox.Text = path;
                            Main.sortForm.Show();
                        }
                    );
                    sortFormOpenItem.MouseUp += showSortFormHandler;
                    selectAllChildrenItem.Visible = true;
                    selectAllChildrenItem.MouseUp -= selectAllChildrenHandler;
                    selectAllChildrenHandler = new MouseEventHandler(
                        (s2, e2) => SelectTreeNodeJsonChildren(node)
                    );
                    selectAllChildrenItem.MouseUp += selectAllChildrenHandler;
                }
                else
                {
                    sortFormOpenItem.Visible = false;
                    selectAllChildrenItem.Visible = false;
                }
                NodeRightClickMenu.Show(MousePosition);
            }
            if (node.IsSelected)
                // normally clicking a node selects it, so we don't need to explicitly invoke this method
                // but if the node was already selected, we need to call it
                Tree_NodeSelection(sender, e);
        }

        /// <summary>
        /// Get the path to the current TreeNode.<br></br>
        /// Style: one of Python, JavaScript), or RemesPath<br></br>
        /// EXAMPLES (using the JSON {"a b": [1, {"c": 2}], "d": [4]}<br></br>
        /// Path to key "c":<br></br>
        /// - JavaScript style: ['a b'][1].c<br></br>
        /// - Python style: ['a b'][1]['c']<br></br>
        /// - RemesPath style: [`a b`][1].c
        /// </summary>
        /// <param name="style"></param>
        /// <returns></returns>
        public string PathToTreeNode(TreeNode node, KeyStyle style = KeyStyle.Python, List<string> path = null)
        {
            if (path == null)
                path = new List<string>();
            if (node.Parent == null)
            {
                if (path.Count == 0)
                    return "";
                path.Reverse(); // cuz they were added from the node to the root
                return string.Join("", path);
            }
            path.Add(KeyOfTreeNode(node, style));
            return PathToTreeNode(node.Parent, style, path);
        }

        /// <summary>
        /// See JNode.FormatKey, but uses the key of a TreeNode
        /// </summary>
        /// <param name="node"></param>
        /// <param name="style"></param>
        /// <returns></returns>
        public string KeyOfTreeNode(TreeNode node, KeyStyle style)
        {
            if (node.Name == "" // TreeNodes representing array members have no name
                // but we need to be careful because an object could have the empty string as a key
                && node.Parent != null && pathsToJNodes[node.Parent.FullPath] is JArray)
            {
                // we get the index of the node this way because the treenodes can
                // be a sparse representation of its JArray, where there is only
                // one treenode for every i^th JNode in the JArray. 
                string[] parts = node.Text.Split(' ', ':');
                int idx = int.Parse(parts[0]);
                return $"[{idx}]";
            }
            return JNode.FormatKey(node.Name, style);
        }

        public void SelectTreeNodeJson(TreeNode node)
        {
            if (Main.activeFname != fname)
                return;
            int nodeStartPos = 0, nodeEndPos = 0;
            if (pathsToJNodes.TryGetValue(node.FullPath, out _))
            {
                nodeStartPos = NodePosInJsonDoc(node);
                nodeEndPos = Main.EndOfJNodeAtPos(nodeStartPos, Npp.editor.GetLength());
            }
            if (nodeStartPos == nodeEndPos)
                MessageBox.Show("The selected tree node does not appear to correspond to a JSON element in the document.",
                    "Couldn't select associated JSON", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
            {
                Npp.editor.ClearSelections();
                Npp.editor.SetSelection(nodeStartPos, nodeEndPos);
                Npp.editor.GrabFocus();
            }
        }

        public void SelectTreeNodeJsonChildren(TreeNode node)
        {
            if (Main.activeFname != fname)
                return;
            if (!pathsToJNodes.TryGetValue(node.FullPath, out JNode jnode))
                MessageBox.Show("The selected tree node does not appear to correspond to a JSON element in the document.",
                    "Couldn't select children of JSON", MessageBoxButtons.OK, MessageBoxIcon.Error);
            int selectionStartPos = ParentSelectionStartPos(node);
            IEnumerable<int> positions;
            if (jnode is JArray arr)
                positions = arr.children.Select(x => selectionStartPos + x.position);
            else if (jnode is JObject obj)
                positions = obj.children.Values.Select(x => selectionStartPos + x.position);
            else
            {
                MessageBox.Show("The selected JSON is not an object or array, and thus has no children.",
                    "Couldn't select children of JSON", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Main.SelectAllChildren(positions);
        }

        /// <summary>
        /// reset the JSON and query of this form to the JSON in whatever
        /// file is currently active
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefreshButton_Click(object sender, EventArgs e)
        {
            shouldRefresh = false;
            string cur_fname = Npp.notepad.GetCurrentFilePath();
            (bool _, JNode new_json, bool _) = Main.TryParseJson();
            if (new_json == null)
                return;
            fname = cur_fname;
            json = new_json;
            queryResult = json;
            JsonTreePopulate(json);
        }

        // close the find/replace form when this becomes invisible
        private void TreeViewer_VisibleChanged(object sender, EventArgs e)
        {
            if (findReplaceForm != null && !findReplaceForm.IsDisposed)
                findReplaceForm.Close();
        }

        /// <summary>
        /// On double-click, activate the buffer with the tree viewer's fname
        /// </summary>
        private void TreeViewer_DoubleClick(object sender, EventArgs e)
        {
            Npp.notepad.OpenFile(fname);
        }

        /// <summary>
        /// Just the filename, no directory information.<br></br>
        /// If no fname supplied, gets the relative filename for this TreeViewer's fname.
        /// </summary>
        public string RelativeFilename(string fname = null)
        {
            if (fname == null) fname = this.fname;
            string[] fname_split = fname.Split('\\');
            return fname_split[fname_split.Length - 1];
        }

        /// <summary>
        /// Change the fname attribute of this.<br></br>
        /// We would like to be able to change the title of the UI element as well,
        /// but it seems pretty hard to do from C#.
        /// </summary>
        /// <param name="new_fname"></param>
        public void Rename(string new_fname)
        {
            fname = new_fname;
        }
    }
}
